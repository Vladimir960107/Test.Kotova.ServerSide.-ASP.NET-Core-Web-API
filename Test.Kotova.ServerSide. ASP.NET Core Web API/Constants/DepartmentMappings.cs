using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Constants
{
    public static class DepartmentMappings
    {
        // Maps department IDs to department DB names
        public static readonly Dictionary<int, string> DepartmentToDBName = new()
        {
            { 1, "TestDB" },
            { 2, "TechnicalDepDB" },
            { 5, "ManagementDB" }
        };

        // Maps DB names to department IDs for reverse lookup if needed
        public static readonly Dictionary<string, int> DBNameToDepartment = new()
        {
            { "TestDB", 1 },
            { "TechnicalDepDB", 2 },
            { "ManagementDB", 5 }
        };

        public static ApplicationDBContextBase GetDbContext(
            int departmentId,
            ApplicationDBContextGeneralConstr generalConstr,
            ApplicationDBContextTechnicalDepartment technicalDep,
            ApplicationDBContextManagement management)
        {
            return departmentId switch
            {
                1 => generalConstr,
                2 => technicalDep,
                5 => management,
                _ => null
            };
        }

        public static ApplicationDBContextBase GetDbContextByDBName(
            string dbName,
            ApplicationDBContextGeneralConstr generalConstr,
            ApplicationDBContextTechnicalDepartment technicalDep,
            ApplicationDBContextManagement management)
        {
            return dbName switch
            {
                "TestDB" => generalConstr,
                "TechnicalDepDB" => technicalDep,
                "ManagementDB" => management,
                _ => null
            };
        }
    }
}
