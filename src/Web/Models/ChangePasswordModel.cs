﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Web.Models
{
    public class ChangePasswordModel
    {
        [Required]
        [DisplayName("Old Password")]
        public string OldPassword;

        [Required]
        [DisplayName("Password")]
        public string Password;
    }
}
