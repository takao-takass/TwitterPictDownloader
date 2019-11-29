using MailKit.Net.Pop3;
using MailKit.Security;
using System;
using TwitterPictDownloader.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net.Http;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using System.Linq;
using System.IO;
using System.Reflection;

namespace TwitterPictDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            // メール受信済み情報を読み込み
            Console.WriteLine("前回のメール受信済み情報を読み込んでいます...");

            // ファイルパスを作成し、読み込みを行う
            var settingFilePath = String.Format(
                "{0}/setting.txt",
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            );
            var lastDateTimeString = String.Empty;
            using (StreamReader sr = new StreamReader(new FileStream(settingFilePath, FileMode.Open)))
            {
                lastDateTimeString = sr.ReadLine();
            }

            // 前回のメール受信日時をDateTime型で生成
            var lastDateTimeList = lastDateTimeString.Split(" ");
            var lastRecvDateTime = new DateTime(
                Int32.Parse(lastDateTimeList[0]),
                Int32.Parse(lastDateTimeList[1]),
                Int32.Parse(lastDateTimeList[2]),
                Int32.Parse(lastDateTimeList[3]),
                Int32.Parse(lastDateTimeList[4]),
                Int32.Parse(lastDateTimeList[5])
            );

            // メールサーバに接続
            // メールを受信してメールリストを作成する
            Console.WriteLine("メールを受信しています...");
            var mailList = new List<MailModel>();
            using (var client = new Pop3Client())
            {
                // メールサーバに接続
                client.Connect(Properties.Resources.host, int.Parse(Properties.Resources.port), SecureSocketOptions.SslOnConnect);
                client.Authenticate(Properties.Resources.address, Properties.Resources.password);

                // メールは最新のものから1件ずつ受信してメールリストを作成する
                // 前回実行時に受信したメールよりも、後に受信したメールのみを対象とする
                for (var count = client.Count - 1; count >= 0; count--)
                {
                    var mimeMessage = client.GetMessage(count);
                    if (mimeMessage.Date.DateTime <= lastRecvDateTime)
                    {
                        break;
                    }

                    mailList.Add(new MailModel()
                    {
                        DateTime = mimeMessage.Date.DateTime,
                        TextBody = mimeMessage.TextBody
                    });
                }
            }

            Console.WriteLine(String.Format("メール受信件数：{0}件", mailList.Count));

            // 受信したメールの本文を解析する
            // twitterのツイートページのURLを探す
            // 下記をリストにする
            // ・ツイートのURL
            // ex) https://twitter.com/xxxxxx/status/1160725269499412481?s=03
            Console.WriteLine("ツイートのURLを抽出しています...");
            var tweetUrlList = new List<String>();
            foreach (var mail in mailList)
            {
                // メール本文は長いことがあるため、改行文字でSplitする。
                var bodyLineList = mail.TextBody.Replace("\r", "\\▼")
                                                .Replace("\n", "\\▼")
                                                .Split("\\▼");

                // 正規表現を用いてURLのパターンに一致する文字列を取得する
                // 取得した文字列はツイートのURLである
                foreach (var bodyLine in bodyLineList)
                {
                    //var tweetUrl = Regex.Match(bodyLine, @"https://twitter.com/[\w/:%#\$&\?\(\)~\.=\+\-]+").Value;
                    var tweetUrl = Regex.Match(bodyLine, @"https://twitter.com/[\w/:%#\$&\?\(\)~\.=\+\-]+/status/[\w/:%#\$&\?\(\)~\.=\+\-]+").Value;
                    if (!String.IsNullOrEmpty(tweetUrl))
                    {
                        tweetUrlList.Add(tweetUrl);
                    }
                }
            }

            Console.WriteLine(String.Format("抽出したツイートURL：{0}件", tweetUrlList.Count));

            // ツイートのURLにアクセスする
            // アクセス先のHTMLを解析して、下記を取得してリストにする
            // ・ユーザ名(＠～～～)
            // ・ツイート本文
            // ・画像のURL(複数あり得るためリスト型)
            Console.WriteLine("ツイートの情報を取得しています...");
            var tweetList = new List<TweetModel>();
            var endCount = 0;
            foreach (var tweetUrl in tweetUrlList)
            {
                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        // ツイートページのHTMLを取得する
                        var html = httpClient.GetStringAsync(tweetUrl).Result;
                        var parsedHtml = (new HtmlParser()).ParseDocument(html);

                        // metaタグのみ取得する
                        var metaTagList = parsedHtml.GetElementsByTagName("meta");
                        var elementList = new List<IElement>();
                        foreach (var metaTag in metaTagList)
                        {
                            elementList.Add(metaTag);
                        }

                        var urlSegmentList = new Uri(tweetUrl).Segments;

                        // ツイート情報をリストに格納する
                        tweetList.Add(new TweetModel()
                        {
                            UserName = urlSegmentList[1].Replace("/", String.Empty),
                            // ツイートID
                            TweetId = urlSegmentList[urlSegmentList.Length - 1],
                            // ツイート本文
                            TweetBody = elementList.Find(element => "og:description".Equals(element.GetAttribute("property")))
                                                   .GetAttribute("content"),
                            // 画像URLのリスト
                            ImageUrlList = elementList.FindAll(element => "og:image".Equals(element.GetAttribute("property")))
                                                      .ConvertAll(element => element.GetAttribute("content"))
                        });

                        Console.WriteLine(String.Format("> {0} / {1}", ++endCount, tweetUrlList.Count));
                    }
                    catch (Exception e)
                    {
                        var exceptionMessage = e.ToString().Split("\n")[0];
                        Console.WriteLine(String.Format("> {0} / {1} 失敗", ++endCount, tweetUrlList.Count));
                        Console.WriteLine(String.Format("> {0}", exceptionMessage));
                    }
                }
            }

            var imageCount = tweetList.ConvertAll(tweet => tweet.ImageUrlList.Count).Sum();
            Console.WriteLine(String.Format("取得したツイート：{0}件", tweetList.Count));
            Console.WriteLine(String.Format("ツイートに含まれる画像：{0}件", imageCount));

            // 画像のURLにアクセスして、HTTPで画像ファイルを取得する。
            // 保存名は、ユーザ名_画像ファイル名_連番。
            // 同時に、ツイートをテキストファイルで保存する。
            // テキストファイルの保存名は、画像と同様(連番は無し)。
            Console.WriteLine("画像をダウンロードしています...");
            var downloadedCount = 0;
            foreach (var tweet in tweetList)
            {
                var imageNumber = 0;
                foreach (var imageUrl in tweet.ImageUrlList)
                {
                    using (var httpClient = new HttpClient())
                    {
                        try
                        {
                            // 画像ファイルの保存パスを作成
                            var fileName = String.Format(
                                "{0}/download/{1}_{2}-{3}.jpg",
                                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                tweet.UserName,
                                tweet.TweetId,
                                imageNumber++
                            );

                            // 画像ファイルを取得
                            var response = httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseContentRead).Result;

                            // 画像をファイルに出力
                            using (var fileStream = File.Create(fileName))
                            using (var httpStream = response.Content.ReadAsStreamAsync().Result)
                            {
                                httpStream.CopyTo(fileStream);
                            }

                            Console.WriteLine(String.Format("> {0} / {1}", ++downloadedCount, imageCount));
                        }
                        catch (Exception e)
                        {
                            var exceptionMessage = e.ToString().Split("\n")[0];
                            Console.WriteLine(String.Format("> {0} / {1} 失敗", ++downloadedCount, imageCount));
                            Console.WriteLine(String.Format("> {0}", exceptionMessage));
                        }
                    }
                }

                // テキストファイルの保存パスを作成
                var txtFileName = String.Format(
                    "{0}/download/{1}_{2}.txt",
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    tweet.UserName,
                    tweet.TweetId
                );

                // ツイート本文をテキストファイルに出力
                File.WriteAllText(txtFileName, tweet.TweetBody);

            }

            // 受信したメールの最後の受信日時を、メール受信済み情報に書き込む
            using (var outputFile = new StreamWriter(settingFilePath))
            {
                foreach(var mail in mailList)
                {
                    var dateTimeString = String.Format("{0} {1} {2} {3} {4} {5} ",
                        mailList[0].DateTime.Year.ToString(),
                        mailList[0].DateTime.Month.ToString(),
                        mailList[0].DateTime.Day.ToString(),
                        mailList[0].DateTime.Hour.ToString(),
                        mailList[0].DateTime.Minute.ToString(),
                        mailList[0].DateTime.Second.ToString()
                    );
                    outputFile.WriteLine(dateTimeString);
                    break;
                }
            }

            // 処理終了
            Console.WriteLine("end");
            Console.ReadKey();
        }
    }
}
