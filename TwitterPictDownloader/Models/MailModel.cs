using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterPictDownloader.Models
{
    /// <summary>
    /// メール情報を保持するモデルクラス
    /// </summary>
    class MailModel
    {
        /// <summary>
        /// 受信日時
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// メール本文
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// メール本文種類(0:Text、1:Html)
        /// </summary>
        public int BodyType { get; set; }
    }
}
