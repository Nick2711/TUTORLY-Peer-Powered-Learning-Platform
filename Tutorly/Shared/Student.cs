namespace Tutorly.Shared
{
    public class Student : User
    {

        private List<Topic> subscribedTopics;

        public Student(bool active, int userId, string email, string name, string password, RoleType role) : base(active, userId, email, name, password, role)
        {
        }

        public List<Module> getEnrolledModules()
        {
            //get from db
            //modulesEnrolled.Add();
            return modulesEnrolled;
        }

        private List<Module> modulesEnrolled;
        //private Queryable query
    }
}
