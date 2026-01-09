using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.InputPorts
{
    public interface IUserService
    {
        public UserType? CheckPasswordAndGetUserType(int id, string inputPassword);
        public bool ChangePassword(int id, string currentPassword, string newPassword);
    }
}
