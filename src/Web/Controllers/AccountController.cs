﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AzureRepositories.User;
using Common.Log;
using Core.Extensions;
using Core.KeyValue;
using Core.User;
using Lykke.Common.Log;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoreLinq;
using Newtonsoft.Json;
using Shared.Settings;
using Web.Code;
using Web.Extensions;
using Web.Models;

namespace Web.Controllers
{
    [Authorize]
    [IgnoreLogAction]
    public class AccountController : BaseController
    {
        private readonly ILog _log;
        private readonly AppSettings _appSettings;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserSignInHistoryRepository _userHistoryRepository;
        private readonly IKeyValuesRepository _keyValuesRepository;

        private string HomeUrl => Url.Action("Repository", "Home");
        private string ApiClientId { get; }
        private string AvailableEmailsRegex { get; }

        public AccountController(
            ILogFactory logFactory,
            AppSettings appSettings,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IUserSignInHistoryRepository userHistoryRepository,
            IUserActionHistoryRepository userActionHistoryRepository,
            IKeyValuesRepository keyValuesRepository
            ) : base(userActionHistoryRepository)
        {
            _log = logFactory.CreateLog(this);
            _appSettings = appSettings;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _userHistoryRepository = userHistoryRepository;
            _keyValuesRepository = keyValuesRepository;

            ApiClientId = _appSettings.ApiClientId;
            AvailableEmailsRegex = _appSettings.AvailableEmailsRegex;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SignIn(string returnUrl)
        {
            try
            {
                // TODO: don't forget to remove this line
                _log.Info("Sign In");

                var users = await _userRepository.GetUsers();

                if (users.Count == 0)
                {
                    var usr = new UserEntity
                    {
                        PasswordHash = _appSettings.DefaultPassword.GetHash(),
                        RowKey = _appSettings.DefaultUserEmail,
                        FirstName = _appSettings.DefaultUserFirstName,
                        LastName = _appSettings.DefaultUserLastName,
                        Active = true,
                        Admin = true
                    };

                    await _userRepository.SaveUser(usr);
                }

                ViewData["ReturnUrl"] = returnUrl;

                return View(new SignInModel { GoogleApiClientId = ApiClientId });
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: returnUrl);
                return View(new SignInModel { GoogleApiClientId = ApiClientId });
            }
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> SignIn(string email, string password, string returnUrl)
        {
            try
            {
                var user = await _userRepository.GetUserByUserEmail(email);

                if (user == null)
                    return View(new SignInModel());

                // temporary commented
                //if (user == null)
                //{
                //    if (!String.IsNullOrWhiteSpace(_appSettings.DefaultUserEmail) && !String.IsNullOrWhiteSpace(_appSettings.DefaultPassword))
                //    {
                //        user = new UserEntity()
                //        {
                //            Active = true,
                //            Admin = true,
                //            FirstName = _appSettings.DefaultUserFirstName,
                //            LastName = _appSettings.DefaultUserLastName,
                //            Salt = String.Empty,
                //            PasswordHash = $"{_appSettings.DefaultPassword}{String.Empty}".GetHash(),
                //            PartitionKey = "U",
                //            RowKey = _appSettings.DefaultUserEmail
                //        };
                //    }
                //    else
                //    {
                //        return View(new SignInModel());
                //    }

                //}

                var passwordHash = $"{password}{user.Salt}".GetHash();
                if (user.PasswordHash != passwordHash)
                    user = null;

                if (user?.Active != null && user.Active.Value)
                {
                    var claims = new List<Claim>
                 {
                     new Claim(ClaimTypes.Sid, email),
                     new Claim("IsAdmin", user.Admin.ToString()),
                     new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim())
                 };

                    var claimsIdentity = new ClaimsIdentity(claims, "password");
                    var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple);
                    await _userHistoryRepository.SaveUserLoginHistoryAsync(user, UserInfo.Ip);

                    return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
                }

                return View(new SignInModel { GoogleApiClientId = ApiClientId });
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: new { email, password, returnUrl });
                return View(new SignInModel { GoogleApiClientId = ApiClientId });
            }
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string oldPassword, string password)
        {
            try
            {
                var user = await _userRepository.GetUserByUserEmail(UserInfo.UserEmail, oldPassword.GetHash());

                if (user == null)
                {
                    ViewData["incorrectPassword"] = true;
                    return View();
                }

                byte[] salt = new byte[128 / 8];

                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }

                user.Salt = Convert.ToBase64String(salt);
                user.PasswordHash = $"{password}{user.Salt}".GetHash();
                await _userRepository.SaveUser(user);

                return Redirect(HomeUrl);
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: new { oldPassword, password });
                return Redirect(HomeUrl);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SignOut()
        {
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Redirect(HomeUrl);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return Redirect(HomeUrl);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ManageUsers()
        {
            try
            {
                var user = await _userRepository.GetUserByUserEmail(UserInfo.UserEmail);

                if (user == null || !(user.Admin.HasValue && user.Admin.Value))
                {
                    return Forbid();
                }

                return View(new ManageUsersModel
                {
                    Users = await GetAllUsers()
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return View(new ManageUsersModel { Users = new List<UserModel>() });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ManageRoles()
        {
            try
            {
                var user = await _userRepository.GetUserByUserEmail(UserInfo.UserEmail);
                if (user == null || !(user.Admin.HasValue && user.Admin.Value))
                {
                    return Forbid();
                }
                var keyValues = await _keyValuesRepository.GetKeyValuesAsync();
                ViewData["keyValueNames"] = JsonConvert.SerializeObject(keyValues.Select(key => key.RowKey).Distinct());
                return View(await GetAllRoles());
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return View(new List<RoleModel>());
            }
        }

        public async Task<IActionResult> SaveUser(UserModel user)
        {
            try
            {
                var usr = (await _userRepository.GetUserByUserEmail(user.Email)) as UserEntity ?? new UserEntity
                {
                    PasswordHash = _appSettings.DefaultPassword.GetHash()
                };

                usr.RowKey = user.Email;
                usr.FirstName = user.FirstName;
                usr.LastName = user.LastName;
                usr.Active = user.Active;
                usr.Admin = user.Admin;

                // get rowKeys for roles by roleNames
                var roles = await _roleRepository.GetAllAsync(x => user.Roles.Contains(x.Name));

                // save rowKeys to user
                usr.Roles = roles.Select(x => x.RowKey).ToArray();

                await _userRepository.SaveUser(usr);
                var result = await GetAllUsers();

                return new JsonResult(new { json = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: user);
                return new JsonResult(new { });
            }
        }

        public async Task<IActionResult> RemoveUser(string userEmail)
        {
            try
            {
                await _userRepository.RemoveUser(userEmail);
                var result = await GetAllUsers();

                return new JsonResult(new { json = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: userEmail);
                return new JsonResult(new { });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveRole(RoleModel role)
        {
            try
            {
                var roleEntity = await _roleRepository.GetAsync(role.RowKey) as RoleEntity ?? new RoleEntity
                {
                    RowKey = Guid.NewGuid().ToString()
                };

                // check for duplications, if role already exists, we must not create it once more
                var duplications = await _roleRepository.GetAllAsync(x => x.RowKey != roleEntity.RowKey && x.Name == role.Name);
                if (duplications.Any())
                {
                    return new JsonResult(new { status = 0 });
                }

                roleEntity.Name = role.Name;
                roleEntity.KeyValues = role.KeyValues != null ? role.KeyValues.DistinctBy(x => x.RowKey).ToArray<IRoleKeyValue>() : new IRoleKeyValue[] { };

                await _roleRepository.SaveAsync(roleEntity);
                var result = await GetAllRoles();

                return new JsonResult(new { status = 1, json = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: role);
                return new JsonResult(new { status = 3 });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveRole(string roleId)
        {
            try
            {
                await _roleRepository.RemoveAsync(roleId);
                var result = await GetAllRoles();

                return new JsonResult(new { json = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: roleId);
                return new JsonResult(new { });
            }
        }

        public async Task<IActionResult> ResetUserPassword(string userEmail)
        {
            try
            {
                if (!(await _userRepository.GetUserByUserEmail(userEmail) is UserEntity user))
                    return new JsonResult(new { result = "User not found" });

                user.PasswordHash = _appSettings.DefaultPassword.GetHash();
                await _userRepository.SaveUser(user);

                return new JsonResult(new { Result = UpdateSettingsStatus.Ok });
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: userEmail);
                return new JsonResult(new { Result = UpdateSettingsStatus.InternalError });
            }
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Authenticate(string googleSignInIdToken, string returnUrl)
        {
            try
            {
                var webSignature = await GoogleJsonWebSignatureEx.ValidateAsync(googleSignInIdToken);

                if (!webSignature.Audience.Equals(ApiClientId))
                {
                    _log.Warning($"{nameof(ApiClientId)} doesn't match.");
                    return Content(string.Empty);
                }
                if (string.IsNullOrWhiteSpace(webSignature.Email) || !webSignature.IsEmailValidated)
                {
                    _log.Warning("Email is empty or not validated.", context: webSignature.Email);
                    return Content(string.Empty);
                }
                if (!Regex.IsMatch(webSignature.Email, AvailableEmailsRegex) )
                {
                    _log.Warning($"Email {webSignature.Email} failed regex validation", context: AvailableEmailsRegex);
                    return Content(string.Empty);
                }

                var user = await _userRepository.GetUserByUserEmail(webSignature.Email);
                if (user == null)
                {
                    _log.Warning("Coudn't find user by email", context: webSignature.Email);
                    return Content(string.Empty);
                }

                if (!user.Active.HasValue || !user.Active.Value)
                {
                    _log.Warning("User is not active.");
                    return Content(string.Empty);
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Sid, webSignature.Email),
                    new Claim("IsAdmin",  user.Admin.ToString()),
                    new Claim(ClaimTypes.Name, webSignature.Email.Trim())
                };

                var claimsIdentity = new ClaimsIdentity(claims, "password");
                var claimsPrinciple = new ClaimsPrincipal(claimsIdentity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrinciple);
                await _userHistoryRepository.SaveUserLoginHistoryAsync(user, UserInfo.Ip);

                return Content(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
            }
            catch (Exception ex)
            {
                _log.Error(ex, context: new { googleSignInIdToken, returnUrl });
                return Content(ex.ToString());
            }
        }

        #region Private Methods
        private async Task<List<UserModel>> GetAllUsers()
        {
            try
            {
                var result = await _userRepository.GetUsers();

                var users = (from u in result
                             let uc = u as UserEntity
                             let ord = uc.Active.HasValue && uc.Active.Value ? 0 : 1
                             orderby ord, uc.RowKey
                             select new UserModel
                             {
                                 Email = uc.RowKey,
                                 FirstName = uc.FirstName,
                                 LastName = uc.LastName,
                                 Active = uc.Active ?? false,
                                 Admin = uc.Admin ?? false,
                                 Roles = uc.Roles
                             }).ToList();

                // iterate through all users and get roleNames by role rowKeys
                foreach (var user in users)
                {
                    if (user.Roles != null)
                    {
                        var roles = await _roleRepository.GetAllAsync(x => user.Roles.Contains(x.RowKey));
                        var roleNames = roles.Select(x => x.Name).ToArray();
                        // assign roleNames to users
                        user.Roles = roleNames;
                    }
                }

                return users;
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return new List<UserModel>();
            }
        }

        private async Task<List<RoleModel>> GetAllRoles()
        {
            try
            {
                var result = await _roleRepository.GetAllAsync();

                return (from r in result
                        let ur = r as RoleEntity
                        orderby ur.RowKey
                        select new RoleModel
                        {
                            RowKey = ur.RowKey,
                            Name = ur.Name,
                            KeyValues = ur.KeyValues as RoleKeyValue[]
                        }).ToList();
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return new List<RoleModel>();
            }
        }
        #endregion
    }
}
