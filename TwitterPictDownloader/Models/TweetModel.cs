using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterPictDownloader.Models
{
    /// <summary>
    /// ツイート情報を保持するモデルクラス
    /// </summary>
    class TweetModel
    {
        /// <summary>
        /// ユーザ名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// ツイートID
        /// </summary>
        public string TweetId { get; set; }

        /// <summary>
        /// ツイート本文
        /// </summary>
        public string TweetBody { get; set; }

        /// <summary>
        /// 画像のURL
        /// </summary>
        public List<string> ImageUrlList { get; set; }

    }
}
