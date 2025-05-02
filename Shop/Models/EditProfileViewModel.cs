using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Shop.Models
{
    public class EditProfileViewModel
    {
        public string PhoneNumber { get; set; }
        public string diachi { get; set; } // Thêm thuộc tính diachi
        public bool HasPassword { get; set; }
        public int LoginsCount { get; set; }
        public bool TwoFactor { get; set; }
    }
}