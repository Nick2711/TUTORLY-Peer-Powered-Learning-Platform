using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tutorly.Shared
{
    public class CurrentUserDto
    {
        public int UserId { get; set; }
        public RoleType Role { get; set; } = RoleType.Student;
    }
}
