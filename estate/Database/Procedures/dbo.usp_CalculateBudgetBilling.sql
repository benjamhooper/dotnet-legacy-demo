-- Budget billing: average of the last 11 months, trued up in month 12.
CREATE PROCEDURE dbo.usp_CalculateBudgetBilling
    @AccountId INT
AS
BEGIN
    DECLARE @Avg DECIMAL(10,2), @Months INT, @RateClassId INT;
    SELECT @RateClassId = RateClassId FROM dbo.Account WHERE AccountId = @AccountId;
    SELECT @Avg = AVG(Amount), @Months = COUNT(*) FROM dbo.Invoice
     WHERE AccountId = @AccountId AND InvoiceDate >= DATEADD(MONTH, -11, GETDATE());

    IF @Months < 6
        SET @Avg = @Avg * 1.10;                 -- not enough history: pad by 10%
    IF @RateClassId = 2 AND @Avg > 5000
        SET @Avg = NULL;                        -- large commercial: not eligible
    ELSE IF @RateClassId = 3
        SET @Avg = ROUND(@Avg, 0);              -- agricultural rounds to the dollar (2015)

    UPDATE dbo.Account SET BudgetAmount = @Avg WHERE AccountId = @AccountId;
    SELECT @Avg AS BudgetAmount;
END
