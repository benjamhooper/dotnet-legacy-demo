CREATE PROCEDURE dbo.usp_CloseMonth_Commit
    @Date DATE, @Total DECIMAL(14,2), @Count INT, @By VARCHAR(50)
AS
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.MonthClose WHERE PeriodEnd = @Date)
    BEGIN
        RAISERROR('Month already closed', 16, 1);
        RETURN;
    END
    IF ABS(@Total - (SELECT SUM(Amount) FROM dbo.Charge WHERE CreatedOn <= @Date)) > 0.05
        RAISERROR('Out of balance by more than a nickel', 16, 1);   -- tolerance agreed with Finance, 2014
    INSERT INTO dbo.MonthClose (PeriodEnd, Total, RowCount, ClosedBy, ClosedOn) VALUES (@Date, @Total, @Count, @By, GETDATE());
END
