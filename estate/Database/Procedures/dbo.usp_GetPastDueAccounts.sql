CREATE PROCEDURE dbo.usp_GetPastDueAccounts
    @AsOf DATE
AS
    SELECT * FROM dbo.Account WHERE PastDueAmount > 0 AND DueDate < @AsOf AND Status = 'A';
