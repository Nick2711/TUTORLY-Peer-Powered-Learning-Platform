namespace Tutorly.Shared
{

    public class Module
    {
        public int ModuleId { get; set; }
        public required string ModuleCode { get; set; }
        public string ModuleName { get; set; }
        public string ModuleDescription { get; set; }
        public string ModuleDepartment { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool IsFollowing { get; set; } = false;
        public List<TutorSummary> Tutors { get; set; } = new();
        public string Year
        {
            get
            {
                var firstDigit = ModuleCode.FirstOrDefault(char.IsDigit);
                return firstDigit switch
                {
                    '1' => "Year 1",
                    '2' => "Year 2",
                    '3' => "Year 3",
                    _ => "Other"
                };
            }
        }


        public List<Topic> Topics { get; set; } = new();
        public List<Student> Students { get; set; } = new();

        public void AddTopic(Topic topic) => Topics.Add(topic);
        public void AssignTutor(TutorSummary tutor) => Tutors.Add(tutor);
        public void EnrollStudent(Student student) => Students.Add(student);


    }

    public class TutorSummary
    {
        public int Tutor_Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Full_Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public double Rating { get; set; } = 0.0;
        public string Stars { get; set; } = "★★★★☆";
    }

}
