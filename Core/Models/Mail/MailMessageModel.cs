﻿using System;
using System.Collections.Generic;
using System.Linq;
using MailKit.Net.Smtp;
using MailKit;
using MimeKit;
using System.Threading.Tasks;

namespace Core.Models
{
    public class MailMessageModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string Recipent { get; set; }

    }
}
