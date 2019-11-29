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
        /// メール本文(テキスト)
        /// </summary>
        public string TextBody { get; set; }
    }
}
