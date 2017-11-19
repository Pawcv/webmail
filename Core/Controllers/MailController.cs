﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Core.Data;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Core.Controllers
{
    [Authorize]
    public class MailController : Controller
    {

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;
        private readonly ApplicationDbContext _dbContext;

        public MailController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<AccountController> logger,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            _dbContext = dbContext;
        }

        // GET: /<controller>/
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                throw new ApplicationException($"User ID was not found in user claims!");
            }

            var user = await _dbContext.Users.Include(appUser => appUser.ImapModel).SingleOrDefaultAsync(appUser => appUser.Id == userId);

            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (user.ImapModel == null)
            {
                return RedirectToAction("SelectImapProvider", "Manage");
            }

            if (!ImapClientModel.ImapClientModelsDictionary.TryGetValue(user.ImapModel.login + user.ImapModel.password, out var model))
            {
                model = new ImapClientModel(user.ImapModel.login,
                    user.ImapModel.password,
                    user.ImapModel.host,
                    user.ImapModel.port,
                    user.ImapModel.useSsl);
            }

            if (!model.IsConnected)
            {
                model.Connect();
                model.ActiveFolder = "INBOX";
            }
            return View("ShowMailsView", model);
        }

        public async Task<IActionResult> ChangeActiveFolder(string folderName)
        {
            folderName = WebUtility.UrlDecode(folderName);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                throw new ApplicationException($"User ID was not found in user claims!");
            }

            var user = await _dbContext.Users.Include(appUser => appUser.ImapModel).SingleOrDefaultAsync(appUser => appUser.Id == userId);

            if (!ImapClientModel.ImapClientModelsDictionary.TryGetValue(user.ImapModel.login + user.ImapModel.password, out var model))
            {
                model = new ImapClientModel(user.ImapModel.login,
                    user.ImapModel.password,
                    user.ImapModel.host,
                    user.ImapModel.port,
                    user.ImapModel.useSsl);
            }

            model.ActiveFolder = folderName;

            return View("ShowMailsView", model);
        }

        [HttpPost]
        public IActionResult SendMail(string login, string password, string message)
        {
            var mailSender = new MailSenderModel(login, password);
            mailSender.Connect();
            mailSender.SendMessage(message);
            mailSender.Disconnect();
            return RedirectToAction("Index");
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult ImapClientTest(string login, string password, string host, int port, bool useSsl)
        {
            var imapClientModel = new ImapClientModel(login, password, host, port, useSsl);
            imapClientModel.Connect();
            imapClientModel.ActiveFolder = "INBOX";
            return View("ShowMailsView", imapClientModel);
        }

        public IActionResult CreateMail()
        {
            MailMessageModel model = new MailMessageModel();
            return View("CreateMail");
        }

        [HttpPost]
        public IActionResult CreateMail(MailMessageModel model)
        {
            model.Connect();
            model.SendMessage();
            model.Disconnect();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<JsonResult> GetMessage(string folderName, int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                throw new ApplicationException($"User ID was not found in user claims!");
            }

            var user = await _dbContext.Users.Include(appUser => appUser.ImapModel).SingleOrDefaultAsync(appUser => appUser.Id == userId);

            if (!ImapClientModel.ImapClientModelsDictionary.TryGetValue(user.ImapModel.login + user.ImapModel.password, out var model))
            {
                model = new ImapClientModel(user.ImapModel.login,
                    user.ImapModel.password,
                    user.ImapModel.host,
                    user.ImapModel.port,
                    user.ImapModel.useSsl);
            }

            string activeFolder = folderName == null ? "INBOX" : folderName;

            MimeKit.MimeMessage message = model.GetMessage(activeFolder, (uint) id);

            return new JsonResult(message.Body.ToString());
        }
    }
}
