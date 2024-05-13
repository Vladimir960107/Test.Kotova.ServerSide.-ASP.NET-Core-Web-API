


using Kotova.CommonClasses;



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
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Net;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Microsoft.Extensions.Configuration;


namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API;
class DBProcessor
{

    private const double DEVIATION = 0.00001;
    private const string tableName_sql_index = "index";
    private const string tableName_sql_names = "names";
    private const string tableName_sql_jobPosition = "job_position";
    private const string tableName_sql_isDriver = "is_driver";
    private const string tableName_sql_BirthDate = "birth_date";
    private const string tableName_sql_gender = "gender";
    private const string tableName_sql_PN = "personnel_number";
    private const string tableName_sql_department = "department";
    private const string tableName_sql_group = "group";

    private const string tableName_sql_MainName = "dbo.TableTest";
    private const string tableName_Instructions_sql = "dbo.Instructions";
    private const string connectionString_server = "localhost";
    private const string connectionString_database = "TestDB";

    public string tableName_pos_users = "users";
    public string columnName_sql_pos_users_username = "username";
    public string columnName_sql_pos_users_PN = "current_personnel_number";

    private const string tableName_sql_USER_instruction_id = "instruction_id";
    private const string tableName_sql_USER_is_instruction_passed = "is_instruction_passed";
    private const string tableName_sql_USER_datePassed = "date_when_passed";
    private const string tableName_sql_INSTRUCTIONS_cause = "cause_of_instruction";
    private const string tableName_sql_USER_whenWasSendByHeadOfDepartment = "when_was_send_to_user";
    private const string tableName_sql_USER_whenWasSendByHeadOfDepartment_UTCTime = "when_was_send_to_user_UTC_Time";

    private const string birthDate_format = "yyyy-MM-dd";

    public string GetConnectionString() // ВЕЗДЕ ПЕРЕПИСАТЬ ЭТУ ФИГНЮ НА То что из Program.cs, чтобы подключение было нормальным
    {
        // Use Windows Authentication for simplicity and security
        return $"Server={connectionString_server};Database={connectionString_database};Integrated Security=True;";
    }

    public async Task ImportDataFromExcelAsync(string connectionString, string excelFilePath)
    {
        using (var workbook = new XLWorkbook(excelFilePath))
        {
            var worksheet = workbook.Worksheet(1);

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        await ProcessWorksheetRowsAsync(worksheet, connection, transaction);
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }

    private async Task ProcessWorksheetRowsAsync(IXLWorksheet worksheet, SqlConnection connection, SqlTransaction transaction)
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
        List<RowData> rowsParsed = new List<RowData>();
        int counter = 0;
        foreach (var row in worksheet.RangeUsed().Rows())
        {
            counter++;
            if (!TryParseRowAndValidate(row, out var rowData, connection, transaction, columnNumbersExcel, counter))
            {
                // Log and skip the row if parsing failed
                continue;
            }
            try
            {
                await InsertRowDataIntoDatabaseAsync(rowData, connection, transaction);
                rowsParsed.Add(rowData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting row data: {ex.Message}");
                throw;
            }
        }
        try
        {
            await CreateTablesForParsedRowsAsync(rowsParsed);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating tables: {ex.Message}");
            throw;
        }

    }

    private async Task CreateTablesForParsedRowsAsync(List<RowData> rowsParsed)
    {
        foreach (var rowData in rowsParsed)
        {
            await CreateTableAsync(rowData.PersonnelNumber);
        }
    }

    public async Task CreateTableAsync(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is null or empty");
        }

        if (!Regex.IsMatch(tableName, @"^[0-9]+$"))
        {
            throw new ArgumentException("Table name must be numeric.");
        }

        string sql = $@"IF EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}')
            BEGIN
              PRINT 'Table {tableName} already exists.';
            END
            ELSE
            BEGIN
              EXEC('CREATE TABLE [' + '{tableName}' + '] (ID int IDENTITY(1,1) PRIMARY KEY, {tableName_sql_USER_instruction_id} INT,
            {tableName_sql_USER_is_instruction_passed} BIT, 
            {tableName_sql_USER_datePassed} DATETIME,
            {tableName_sql_USER_whenWasSendByHeadOfDepartment} DATETIME,
            {tableName_sql_USER_whenWasSendByHeadOfDepartment_UTCTime} DATETIME);')
              PRINT 'Table {tableName} created successfully!';
            END";

        using (SqlConnection conn = new SqlConnection(GetConnectionString()))
        {
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            await conn.OpenAsync();
            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    Console.WriteLine(reader[0].ToString());
                }
                reader.Close();
            }
        }
    }

    class RowData
    {
        public string? Name { get; set; }

        public string? JobPosition { get; set; }
        public Int16 IsDriver { get; set; }
        public DateTime BirthDate { get; set; }
        public Int16 Gender { get; set; }
        public string? PersonnelNumber { get; set; }
        public string? Department { get; set; }
        public string? Group { get; set; }
        // Add other properties as needed...
    }

    public List<Tuple<string, string>> GetNames(string connectionString)
    {
        List<Tuple<string, string>> names = new List<Tuple<string, string>>(); // Prepare a list to store the retrieved names

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open(); // Open the database connection
            var query = $"SELECT {tableName_sql_names},{tableName_sql_BirthDate} FROM {tableName_sql_MainName}"; // SQL query to retrieve names

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) // Read each row returned by the query
                    {
                        var name = reader[tableName_sql_names] as string; // Safely cast to string, which will be null if the value is DBNull
                        if (reader[tableName_sql_BirthDate] != DBNull.Value)
                        {
                            DateTime? birthDate = (DateTime?)reader[tableName_sql_BirthDate]; // Correct casting of DateTime
                            string birthDateString = birthDate.HasValue ? birthDate.Value.ToString(birthDate_format) : null;

                            if (name != null && birthDateString != null)
                            {
                                names.Add(new Tuple<string, string>(name, birthDateString));
                            }
                        }
                        else
                        {
                            // Optionally handle or log null values here
                        }

                    }
                    reader.Close();
                }
            }
        }

        return names; // Return the list of names
    }
    public List<Instruction> GetInstructions(string connectionString) //This function is similar to GetNames
    {
        var instructions = new List<Instruction>();
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            var query = $"SELECT {tableName_sql_INSTRUCTIONS_cause} FROM {tableName_Instructions_sql}";

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {

                        var name = reader[tableName_sql_INSTRUCTIONS_cause] as string;
                        if (name != null)
                        {
                            Instruction notification = new Instruction(name);
                            instructions.Add(notification);

                        }
                        else
                        {
                            // Optionally handle or log null values here
                        }
                    }
                    reader.Close();
                }
            }
        }

        return instructions;
    }

    public List<Instruction> getNotificationsByUserInDataBase(string userName)
    {
        var instructions = new List<Instruction>();
        using (var connection = new SqlConnection(connectionString_database))
        {
            connection.Open();
            var query = $"SELECT {tableName_sql_INSTRUCTIONS_cause} FROM {tableName_Instructions_sql}";

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {

                        var name = reader[tableName_sql_INSTRUCTIONS_cause] as string;
                        if (name != null)
                        {
                            Instruction notification = new Instruction(name);
                            instructions.Add(notification);

                        }
                        else
                        {
                            // Optionally handle or log null values here
                        }
                    }
                    reader.Close();
                }
            }
        }

        return instructions;
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
        if (!CheckIfAlreadyExistsInDB(connection, transaction, tableName_sql_MainName, tableName_sql_PN, personnelNumber_10ZeroesString))
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


    private async Task InsertRowDataIntoDatabaseAsync(RowData rowData, SqlConnection connection, SqlTransaction transaction)
    {
        using (var command = new SqlCommand())
        {
            command.Connection = connection;
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO {tableName_sql_MainName} ({tableName_sql_names}, {tableName_sql_jobPosition}, {tableName_sql_isDriver}, {tableName_sql_department}, [{tableName_sql_group}], {tableName_sql_BirthDate}, {tableName_sql_gender}, {tableName_sql_PN}) VALUES (@name, @jobPosition, @isDriver, @department, @group, @birthDate, @gender, @personnelNumber)";

            command.Parameters.AddWithValue("@name", rowData.Name);
            command.Parameters.AddWithValue("@jobPosition", rowData.JobPosition);
            command.Parameters.AddWithValue("@isDriver", rowData.IsDriver);
            command.Parameters.AddWithValue("@department", rowData.Department);
            command.Parameters.AddWithValue("@group", rowData.Group);
            command.Parameters.AddWithValue("@birthDate", rowData.BirthDate);
            command.Parameters.AddWithValue("@gender", rowData.Gender);
            command.Parameters.AddWithValue("@personnelNumber", rowData.PersonnelNumber);

            await command.ExecuteNonQueryAsync();
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

    private bool CheckIfAlreadyExistsInDB(SqlConnection connection, SqlTransaction transaction, string tableName, string columnName, object valueToCheck) //just to check whether the index already exist in the database (method)
    {
        
        string query = $"SELECT COUNT(*) FROM {tableName} WHERE [{columnName}] = @numberToCheck";

        using (SqlCommand command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("@numberToCheck", valueToCheck);
            int result = Convert.ToInt32(command.ExecuteScalar());
            return result == 0; // return true if the value does not exist in the database
        }
    }
    
    public async Task<bool> ProcessDataAsync(InstructionPackage package) 
    {
        try
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var instructionId = await FindInstructionIdAsync(package.InstructionCause, connection, transaction);
                        if (instructionId == null)
                        {
                            Console.WriteLine("Couldn't find instruction Id by its name");
                            transaction.Rollback();
                            return false;
                            
                        }
                        int instructionId_NonNull = instructionId ?? default(int);

                        List<string> personelNumbers = await FindPNsOfNamesAndBirthDates(package.NamesAndBirthDates, connection, transaction);
                        if (personelNumbers.Count == 0)
                        {
                            Console.WriteLine("Couldn't find personelNumbers by full names");
                            transaction.Rollback();
                            return false;
                        }
                        var isEverythingFine = await SendNotificationToPeopleAsync(personelNumbers, instructionId_NonNull, connection, transaction);

                        if (!isEverythingFine) 
                        {
                            Console.WriteLine("Couldn't add instruction to names");
                            transaction.Rollback();
                            return false;
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return false;
        }
    }

    public async Task<int?> FindInstructionIdAsync(string? instructionCause, SqlConnection connection, SqlTransaction transaction)
    {
        int? instructionId = null;
        string query = $"SELECT {tableName_sql_USER_instruction_id} FROM {tableName_Instructions_sql} WHERE {tableName_sql_INSTRUCTIONS_cause} = @Cause";
        using (SqlCommand command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddWithValue("@Cause", instructionCause);

            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    instructionId = reader.GetInt32(0);
                }
                reader.Close(); // Explicitly close the reader if not using 'using' statement
            }
        }
        return instructionId;
    }

    private async Task<List<string>> FindPNsOfNamesAndBirthDates(List<Tuple<string, string>>? namesAndBirthDatesString, SqlConnection connection, SqlTransaction transaction)
    {
        if (namesAndBirthDatesString is null || !namesAndBirthDatesString.Any()) { throw new ArgumentException("namesAndBirthDatesString is empty!"); }
        (List<string> names, List<DateTime> birthDates) = DeconstructNamesAndBirthDates(namesAndBirthDatesString);
        string query = $"SELECT {tableName_sql_PN} FROM {tableName_sql_MainName} WHERE {tableName_sql_names} = @name AND {tableName_sql_BirthDate} = @birthDate";

        List<string> PersonalNumbers = new List<string>();

        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            DateTime birthDate = birthDates[i];

            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@birthDate", birthDate);

                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        PersonalNumbers.Add(reader.GetString(0)); // Assuming PN is a string
                    }
                    reader.Close(); // It's safe to explicitly close here
                }
            }
        }
        return PersonalNumbers;
    }

    private (List<string>, List<DateTime>) DeconstructNamesAndBirthDates(List<Tuple<string, string>> namesAndBirthDatesString)
    {
        List<string> names = new List<string>();
        List <DateTime> birthDates = new List<DateTime>();
        foreach (var item in namesAndBirthDatesString)
        {
            try
            {
                // Parse the date from the string
                DateTime date = DateTime.ParseExact(item.Item2, birthDate_format, CultureInfo.InvariantCulture);

                names.Add(item.Item1);
                birthDates.Add(date);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Invalid date format for: {item.Item1} with date {item.Item2}");
                Console.WriteLine(ex.Message);
                throw;
            }
        }
        return (names, birthDates);
    }

    private async Task<bool> SendNotificationToPeopleAsync(List<string> personelNumbers, int instructionId, SqlConnection connection, SqlTransaction transaction)
    {
        try
        {
            foreach (string personelNumber in personelNumbers)
            {
                string tableName = $"[dbo].[{personelNumber}]";
                //Console.WriteLine(personelNumber);
                string query = $"INSERT INTO {tableName} ({tableName_sql_USER_instruction_id}, {tableName_sql_USER_is_instruction_passed}, " +
                    $"{tableName_sql_USER_whenWasSendByHeadOfDepartment}, {tableName_sql_USER_whenWasSendByHeadOfDepartment_UTCTime})" +
                    $" VALUES(@instructionId, @falseValue,@whenWasSendToUser, @whenWasSendToUserUTC)";

                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                using (SqlCommand command = new SqlCommand(query, connection, transaction))
                {
                    command.Parameters.AddWithValue("@instructionId", instructionId);
                    command.Parameters.AddWithValue("@falseValue", false);
                    command.Parameters.AddWithValue("@whenWasSendToUser", DateTime.Now);
                    command.Parameters.AddWithValue("@whenWasSendToUserUTC", DateTime.UtcNow);
                    await command.ExecuteNonQueryAsync();
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            // Log error and rollback transaction if something goes wrong
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
        
}


