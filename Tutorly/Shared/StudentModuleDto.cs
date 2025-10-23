namespace Tutorly.Shared
{
    public class StudentModuleDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int TutorsCount { get; set; }
        public List<StudentModuleTutor> Tutors { get; set; } = new();
    }

    public class StudentModuleTutor
    {
        public int TutorId { get; set; }
        public string TutorName { get; set; } = string.Empty;
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string TutorPhoto { get; set; } = string.Empty;
        public double Rating { get; set; } = 0.0;
        public string Stars { get; set; } = "★★★★☆";
    }

    // Helper classes for view data deserialization
    public class StudentModuleViewData
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int TutorsCount { get; set; }
        public string Tutors { get; set; } = string.Empty;
    }

    public class StudentModuleTutorView
    {
        public int TutorId { get; set; }
        public string TutorName { get; set; } = string.Empty;
    }

    public class ModuleTutorInfo
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string TutorPhoto { get; set; } = string.Empty;
        public double Rating { get; set; } = 0.0;
    }
}
