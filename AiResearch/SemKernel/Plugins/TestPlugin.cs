using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace SemKernel.Plugins;

public class ComplexParameter
{
    public required string Parameter1 { get; set; }
    public required string Parameter2 { get; set; }
}

public class EmployeeRecord
{
    [Description("Name of the employee")]
    [JsonPropertyName("Name")]
    public required string Name { get; set; }
    
    [Description("Employee ID")]
    [JsonPropertyName("ID")]
    public required int Id { get; set; }
    
    [Description("Employee Department")]
    [JsonPropertyName("Department")]
    public required string Department { get; set; }
}

public class ToolResponse
{
    public required bool Success { get; set; }
}

public class TestPlugin
{
    public string TestTool(ComplexParameter parameter)
    {
        return "Hello, World!";
    }

    [KernelFunction("create_employee_record")]
    [Description("Creates a new employee record in the Employee Details application")]
    //public ToolResponse CreateEmployeeRecord(EmployeeRecord record)
    public ToolResponse CreateEmployeeRecord(
        [Description("Name of the employee")]
        string name,
        
        [Description("Employee ID")]
        int id,
        
        [Description("Employee Department")]
        string department
    )
    {
        return new ToolResponse { Success = true };
    }
}