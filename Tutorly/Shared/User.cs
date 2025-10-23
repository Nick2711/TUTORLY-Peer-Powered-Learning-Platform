
using System.Collections.Concurrent;
using System.Xml;

namespace Tutorly.Shared
{
    public abstract class User
    {
        public User(bool active, int userId, string email, string name, string password, RoleType role)
        {
            Active = active;
            this.userId = userId;
            Email = email;
            this.name = name ?? throw new ArgumentNullException(nameof(name));
            this.password = password ?? throw new ArgumentNullException(nameof(password));
        }

        public bool Active; //for sessions
        public required int UserID { get; set; }
        public required string Email { get; set; }
        public required string Name { get; set; }
        public required RoleType Role { get; set; }



        public void Login()
        {
            //login functionality done

        }

        public void updateProfile(string updateName, string updatePassword, RoleType updateRole)
        {
            UserID = UserID;
            name = updateName;
            password = updatePassword;
            role = updateRole;
        }




        private int userId;
        private int email;
        private string name;
        private string password;
        private RoleType role;



        ///private Course course;
    }
}
