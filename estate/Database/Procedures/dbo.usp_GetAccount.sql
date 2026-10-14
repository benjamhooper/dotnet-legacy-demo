CREATE PROCEDURE dbo.usp_GetAccount
    @AccountId INT
AS
    SELECT AccountId, AccountNumber, CustomerName, RateClassId, DueDate, PastDueAmount, CurrentBalance,
           AutoPay, HardshipProgram, LateCount12Months, LastPaymentDate, Status
      FROM dbo.Account
     WHERE AccountId = @AccountId;
