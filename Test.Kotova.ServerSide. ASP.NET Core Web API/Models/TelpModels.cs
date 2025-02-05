namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models
{
    public class TelpEmployee
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public int? DepartmentId { get; set; }
        public int? PositionId { get; set; }
        public string Email { get; set; }
        public string PersonnelNumber { get; set; }

        // Navigation properties
        public virtual TelpDepartment Department { get; set; }
        public virtual TelpPosition Position { get; set; }
    }

    public class TelpDepartment
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Navigation property
        public virtual ICollection<TelpEmployee> Employees { get; set; }
    }

    public class TelpPosition
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Navigation property
        public virtual ICollection<TelpEmployee> Employees { get; set; }
    }
}