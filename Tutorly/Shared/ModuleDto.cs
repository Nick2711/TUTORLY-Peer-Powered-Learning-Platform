namespace Tutorly.Shared
{
    public class ModuleDto
    {
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string ModuleDescription { get; set; } = string.Empty;

        // Computed counts for admin display
        public int UserCount { get; set; }
        public int ResourceCount { get; set; }
        public int TutorCount { get; set; }

        // Derived properties from module code
        public string ModuleDepartment
        {
            get
            {
                var code = ModuleCode.ToUpper();
                if (code.StartsWith("MTH") || code.StartsWith("MAT") || code.StartsWith("MATH"))
                    return "Mathematics";
                if (code.StartsWith("PHY") || code.StartsWith("PHYS"))
                    return "Physics";
                if (code.StartsWith("CS") || code.StartsWith("CSC") || code.StartsWith("CSE") ||
                    code.StartsWith("COMP") || code.StartsWith("CIS") || code.StartsWith("IT"))
                    return "Computer Science";
                if (code.StartsWith("ENG") || code.StartsWith("MECH") || code.StartsWith("CIVIL") ||
                    code.StartsWith("ELEC") || code.StartsWith("CHEM"))
                    return "Engineering";
                if (code.StartsWith("BUS") || code.StartsWith("MGT") || code.StartsWith("ECON") ||
                    code.StartsWith("FIN") || code.StartsWith("MKT"))
                    return "Business";
                return "General";
            }
        }

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

        public string SubjectBadgeClass
        {
            get
            {
                return ModuleDepartment switch
                {
                    "Mathematics" => "badge-sky",
                    "Physics" => "badge-green",
                    "Computer Science" => "badge-purple",
                    "Engineering" => "badge-blue",
                    "Business" => "badge-amber",
                    _ => "badge-gray"
                };
            }
        }

        public string IconClass
        {
            get
            {
                return ModuleDepartment switch
                {
                    "Physics" => "beaker",
                    "Computer Science" => "chip",
                    "Engineering" => "gear",
                    "Business" => "briefcase",
                    _ => "book"
                };
            }
        }
    }

    public class CreateModuleRequest
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string ModuleDescription { get; set; } = string.Empty;
    }

    public class UpdateModuleRequest
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string ModuleDescription { get; set; } = string.Empty;
    }
}
