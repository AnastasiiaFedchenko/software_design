using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.InputPorts;
using Domain.OutputPorts;

namespace Domain
{
    public class UserService: IUserService
    {
        private readonly IUserRepo _userRepo;

        public UserService(IUserRepo userRepo)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        }
        public UserType? CheckPasswordAndGetUserType(int Id, string password)
        {
            return _userRepo.CheckPasswordAndGetUserType(Id, password);
        }
    }
}
