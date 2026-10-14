CREATE PROCEDURE dbo.usp_ApplyPayment
    @AccountId INT, @Amount DECIMAL(10,2), @Channel VARCHAR(10), @On DATETIME
AS
BEGIN
    DECLARE @Remaining DECIMAL(10,2) = @Amount, @InvoiceId INT, @Open DECIMAL(10,2);
    DECLARE inv CURSOR FOR
        SELECT InvoiceId, Amount - AmountPaid FROM dbo.Invoice
         WHERE AccountId = @AccountId AND Amount > AmountPaid ORDER BY DueDate;
    OPEN inv;
    FETCH NEXT FROM inv INTO @InvoiceId, @Open;
    WHILE @@FETCH_STATUS = 0 AND @Remaining > 0
    BEGIN
        IF @Open > @Remaining SET @Open = @Remaining;
        UPDATE dbo.Invoice SET AmountPaid = AmountPaid + @Open WHERE InvoiceId = @InvoiceId;
        SET @Remaining = @Remaining - @Open;
        FETCH NEXT FROM inv INTO @InvoiceId, @Open;
    END
    CLOSE inv; DEALLOCATE inv;
    IF @Remaining > 0
        UPDATE dbo.Account SET CreditBalance = CreditBalance + @Remaining WHERE AccountId = @AccountId;
    IF @Channel = 'CHECK' AND DATEPART(WEEKDAY, @On) IN (1,7)
        UPDATE dbo.Payment SET PostedOn = DATEADD(DAY, 2, @On) WHERE AccountId = @AccountId AND PostedOn = @On;
END
