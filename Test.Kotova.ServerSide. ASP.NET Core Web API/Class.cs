namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API;
using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.AdditionalCharacteristics;
using DocumentFormat.OpenXml.Drawing.Charts;
using System.Collections.Generic;
using System.Transactions;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Presentation;
using System.Reflection;


class ImportFromExcelIntoDB
{

    private const double DEVIATION = 0.00001;
    private const string tableName_sql_index = "index";
    private const string tableName_sql_names = "names";
    private const string tableName_sql_jobPosition = "job_position";
    private const string tableName_sql_isDriver = "isDriver";
    private const string tableName_sql_BirthDate = "BirthDate";
    private const string tableName_sql_gender = "gender";
    private const string tableName_sql_PN = "personnel_number";
    private const string tableName_sql_department = "department";
    private const string tableName_sql_group = "group";

    private const string tableName_sql = "dbo.TableTest";
    /*
    static void Main(string[] args)
    {
        string connectionString = GetConnectionString();
        string excelFilePath = GetExcelFilePath();

        try
        {
            ImportDataFromExcel(connectionString, excelFilePath);
            Console.WriteLine("Data imported successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during the import: {ex.Message}");
        }
    }
    */

    public string GetConnectionString()
    {
        string server = "localhost";
        string database = "TestDB";

        // Use Windows Authentication for simplicity and security
        return $"Server={server};Database={database};Integrated Security=True;";
    }

    private string GetExcelFilePath()
    {
        string[] excelFilePaths = { @"C:\", "Users", "hifly", "Desktop", "Котова", "TEST Для базы данных.xlsx" };
        return Path.Combine(excelFilePaths);
    }
    public string GetTableName()
    {
        return "dbo.TableTest";
    }

    public void ImportDataFromExcel(string connectionString, string excelFilePath)
    {
        using (var workbook = new XLWorkbook(excelFilePath))
        {
            var worksheet = workbook.Worksheet(1);



            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        ProcessWorksheetRows(worksheet, connection, transaction);
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw; // Re-throw the exception to be handled by the caller of ImportDataFromExcel
                    }
                }
            }
        }
    }

    private void ProcessWorksheetRows(IXLWorksheet worksheet, SqlConnection connection, SqlTransaction transaction)
    {
        var columnNumbersExcel = new Dictionary<string, int>
        {
            { "name", 2 },
            { "jobPosition", 3 },
            { "isDriver", 4 },
            { "department", 5 },
            { "group", 6 },
            { "birthDate", 7 },
            { "gender", 8 },
            { "personnelNumber", 9 },
        };
        int counter = 0;
        foreach (var row in worksheet.RangeUsed().Rows())
        {
            counter++;
            if (!TryParseRowAndValidate(row, out var rowData, connection, transaction, columnNumbersExcel, counter))
            {
                // Log and skip the row if parsing failed
                continue;
            }

            InsertRowDataIntoDatabase(rowData, connection, transaction);
        }
    }
    //here put the function needed. for sync names with db

    class RowData
    {
        public string Name { get; set; }

        public string JobPosition { get; set; }
        public Int16 IsDriver { get; set; }
        public DateTime BirthDate { get; set; }
        public Int16 Gender { get; set; }
        public string PersonnelNumber { get; set; }
        public string Department { get; set; }
        public string Group { get; set; }
        // Add other properties as needed...
    }
    bool TryParseRowAndValidate(IXLRangeRow row, out RowData rowData, SqlConnection connection, SqlTransaction transaction, Dictionary<string, int> columnNumbers, int counter)
    {
        try
        {
            string? name_string = row.Cell(columnNumbers["name"]).GetValue<string>();
            string? jobPosition_string = row.Cell(columnNumbers["jobPosition"]).GetValue<string>();
            string? isDriver_string = row.Cell(columnNumbers["isDriver"]).GetValue<string>();
            string? department_string = row.Cell(columnNumbers["department"]).GetValue<string>();
            string? group_string = row.Cell(columnNumbers["group"]).GetValue<string>();
            string? birthDate_string = row.Cell(columnNumbers["birthDate"]).GetValue<string>();
            string? gender_string = row.Cell(columnNumbers["gender"]).GetValue<string>();
            string? personnelNumber_string = row.Cell(columnNumbers["personnelNumber"]).GetValue<string>();
            rowData = new RowData
            {
                Name = CheckIfNotNullAndReturnSameResult(name_string),
                JobPosition = CheckIfNotNullAndReturnSameResult(jobPosition_string),
                IsDriver = CheckIsDriver(isDriver_string),
                Department = CheckIfNotNullAndReturnSameResult(department_string),
                Group = CheckIfNotNullAndReturnSameResult(group_string),
                BirthDate = BirthDateCheck(birthDate_string),
                Gender = GenderCheck(gender_string),
                PersonnelNumber = PersonnelNumberCheck(personnelNumber_string, connection, transaction),
            };

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse row {counter}: {ex.Message}");
            rowData = null;
            return false;
        }
    }
    private Int16 CheckIsDriver(string isDriver_string)
    {
        Int16 isDriver_bit = 0;
        string isDriver_lowerCase = isDriver_string.ToLower();
        switch (isDriver_lowerCase)
        {
            case "да":
            case "да.":
            case "yes":
            case "yes.":
            case "+":
                isDriver_bit = 1;
                break;
            case "нет":
            case "нет.":
            case "no":
            case "no.":
            case "-":
                isDriver_bit = 0;
                break;
            default:
                throw new ArgumentException($"Unknown isDriver state: {isDriver_string}. null returned");

        }
        return isDriver_bit;
    }
    private DateTime BirthDateCheck(string birthDate)
    {

        DateTime birthDateDateTime = default;

        // Include multiple formats to account for potential presence of time. НЕ ПОДУМАЛ О РАЗНЫХ ЧАСОВЫХ ПОЯСАХ В РАЗНЫЕ ДАТЫ.
        var dateFormats = new[] { "d/M/yyyy", "dd.MM.yyyy H:mm:ss", "d/M/yyyy H:mm:ss" };
        if (!(DateTime.TryParseExact(birthDate, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDateDateTime)))
        {
            throw new ArgumentException($"Date format for {birthDate} is incorrect. Skipping row.");
        }
        return birthDateDateTime;
    }
    private Int16 GenderCheck(string gender)
    {
        string gender_lowerCase = gender.ToLower();

        Int16 gender_int = 0;

        switch (gender_lowerCase)
        {
            case "муж.":
            case "муж":
            case "мужчина":
            case "мужской":
                gender_int = 1;
                break;
            case "жен.":
            case "жен":
            case "женщина":
            case "женский":
                gender_int = 2;
                break;
            default:
                throw new ArgumentException($"Unknown gender: {gender}");
        }
        return gender_int;
    }
    private string PersonnelNumberCheck(string personnelNumber_string, SqlConnection connection, SqlTransaction transaction)
    {
        int personnelNumber = ParseFromStringToInt(personnelNumber_string);
        if (personnelNumber / (int)Math.Pow(10, 10) > DEVIATION)
        {
            throw new ArgumentException("personnelNumber is too big");
        }
        string personnelNumber_10ZeroesString = PadNumberWith10Zeroes(personnelNumber);
        if (!CheckIfAlreadyExistsInDB(connection, transaction, tableName_sql, tableName_sql_PN, personnelNumber_10ZeroesString))
        {
            throw new ArgumentException($"personnelNumber {personnelNumber_10ZeroesString} already exist in DB");
        }
        return personnelNumber_10ZeroesString;
    }

    private int ParseFromStringToInt(string str)
    {
        int index_typeInt = 0;
        if (!int.TryParse(str, out index_typeInt))
        {
            throw new ArgumentException("couldn't parse str to int in ParseFromStringToInt");
        }
        return index_typeInt;

    }


    private void InsertRowDataIntoDatabase(RowData rowData, SqlConnection connection, SqlTransaction transaction)
    {
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction; // Assign the transaction
            var builder = new SqlInsertCommandBuilder(tableName_sql);
            builder.AddColumnValue(tableName_sql_names, rowData.Name);
            builder.AddColumnValue(tableName_sql_BirthDate, rowData.BirthDate);
            builder.AddColumnValue(tableName_sql_PN, rowData.PersonnelNumber);
            builder.AddColumnValue(tableName_sql_gender, rowData.Gender); //Светлана Котова сказала, что пол не нужен. Можно убрать, если что.
            builder.AddColumnValue(tableName_sql_jobPosition, rowData.JobPosition);
            builder.AddColumnValue(tableName_sql_department, rowData.Department);
            builder.AddColumnValue(tableName_sql_group, rowData.Group);
            builder.AddColumnValue(tableName_sql_isDriver, rowData.IsDriver);

            builder.ApplyToCommand(command); // This line prepares the command text and parameters
            command.ExecuteNonQuery(); // Execute the command
        }
    }
    

    public class SqlInsertCommandBuilder
    {
        private string tableName;
        private Dictionary<string, object> columnValueMappings;

        public SqlInsertCommandBuilder(string tableName)
        {
            this.tableName = tableName;
            columnValueMappings = new Dictionary<string, object>();
        }

        public void AddColumnValue(string columnName, object value)
        {
            columnValueMappings[columnName] = value;
        }

        public void ApplyToCommand(SqlCommand command)
        {
            StringBuilder columnsPart = new StringBuilder();
            StringBuilder parametersPart = new StringBuilder();

            foreach (var mapping in columnValueMappings)
            {
                if (columnsPart.Length > 0)
                {
                    columnsPart.Append(", ");
                    parametersPart.Append(", ");
                }

                columnsPart.Append($"[{mapping.Key}]");
                parametersPart.Append($"@{mapping.Key}"); // Use the column name as the parameter name

                // Add the parameter to the command
                command.Parameters.AddWithValue($"@{mapping.Key}", mapping.Value);
            }

            command.CommandText = $"INSERT INTO {tableName} ({columnsPart}) VALUES ({parametersPart})";
        }
    }
    private string CheckIfNotNullAndReturnSameResult(string? something)
    {
        if (something is null)
        {
            throw new Exception($"{nameof(something)} is null. Skipping row");
        }
        return something;
    }
    private string PadNumberWith10Zeroes(int number)
    {
        return number.ToString("D10");
    }

    //just to check whether the index already exist in the database (method)
    private bool CheckIfAlreadyExistsInDB(SqlConnection connection, SqlTransaction transaction, string tableName, string columnName, object valueToCheck)
    {
        string query = $"SELECT COUNT(*) FROM {tableName} WHERE [{columnName}] = @numberToCheck";

        using (SqlCommand command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("@numberToCheck", valueToCheck);
            int result = Convert.ToInt32(command.ExecuteScalar());
            return result == 0; // return true if the value does not exist in the database
        }
    }
}
