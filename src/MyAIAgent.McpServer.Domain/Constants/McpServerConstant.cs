namespace MyAIAgent.McpServer.Domain.Constants;

public static class McpServerConstant
{
    public const string CreateCustomerToolName = "create_customer";
    public const string CreateCustomerToolDescription = "Creates a new customer in the system.";
    public const string GetCustomerToolName = "get_all_customers";
    public const string GetCustomerToolDescription = "Retrieves or gets existing customers in the system.";
    public const string GetCustomerByNameToolName = "get_customers_by_name";

    public const string GetCustomerByNameToolDescription =
        "Retrieves or gets existing customers that have name field matching input in the system.";

    public const string DeleteCustomerToolName = "delete_customer";
    public const string DeleteCustomerToolDescription = "Deletes an existing customer in the system.";
    public const string UpdateCustomerToolName = "update_customer";
    public const string UpdateCustomerToolDescription = "Updates an existing customer in the system.";
    public const string AskBackendStructureDocumentToolName = "ask_backend_structure";

    public const string AskBackendStructureDocumentToolDescription =
        "Analyze the backend project structure document and generate application code based on its content.";

    public const string AskBackendTestsDocumentToolName = "ask_backend_tests";

    public const string AskBackendTestsDocumentToolDescription =
        "Analyze the unit testing guidelines document and generate corresponding unit test code for the application components.";

    public const string CustomerIdPropertyDescription = "The identity number of the customer.";
    public const string CustomerNamePropertyDescription = "The name of the customer.";
    public const string CustomerEmailPropertyDescription = "The email address of the customer.";
    public const string CustomerPhonePropertyDescription = "The phone number of the customer.";
    public const string CustomerAddressPropertyDescription = "The address of the customer.";
    public const string CustomerAvatarPropertyDescription = "The avatar link of the customer.";
    public const string QuestionPropertyName = "question";
    public const string QuestionPropertyDescription = "The natural-language query to the content of uploaded document.";
    public const string PropertyTypeString = "string";
    public const string NoCustomersFoundMessage = "No customer found";
    public const string ErrorRetrieveCustomerData = "Error retrieving customer data";
    public const string ErrorCustomerNotCreated = "Customer not created";
    public const string ErrorCustomerNotUpdated = "Customer not updated";
    public const string ErrorCustomerNotDeleted = "Customer not deleted";
    public const string ErrorAskQuestionDocumentFailed = "Ask question document failed";
}