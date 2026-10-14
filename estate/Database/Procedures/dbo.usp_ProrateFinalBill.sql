-- =============================================
-- Author:      (left 2016)
-- Create date: 2012-07-02
-- Description: Final bill when a customer moves out.
--              Called by BillingManager, the CSR web app, the Fixer, and the nightly batch.
-- Changes:     2013-04  small balances under $1.00 written off (both directions)
--              2015-10  deposit applied before credits (was after)
--              2019-01  "temporary" 30-day month for proration, pending tariff review
-- =============================================
CREATE PROCEDURE dbo.usp_ProrateFinalBill
    @AccountId              INT,
    @CycleStart             DATE,
    @MoveOut                DATE,
    @MonthlyCustomerCharge  DECIMAL(10,2),
    @UsageKwh               DECIMAL(12,3),
    @RatePerKwh             DECIMAL(10,5),
    @DepositHeld            DECIMAL(10,2),
    @Credits                DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Days         INT           = DATEDIFF(DAY, @CycleStart, @MoveOut) + 1;
    DECLARE @DailyCharge  DECIMAL(10,2) = @MonthlyCustomerCharge / 30;        -- rounds to the cent, per day
    DECLARE @BaseCharge   DECIMAL(10,2) = @DailyCharge * @Days;
    DECLARE @UsageCharge  DECIMAL(10,2) = @UsageKwh * @RatePerKwh;
    DECLARE @Total        DECIMAL(10,2) = @BaseCharge + @UsageCharge - @DepositHeld - @Credits;

    IF (@Total > 0 AND @Total < 1.00) OR (@Total < 0 AND @Total > -1.00)
        SET @Total = 0;                                                        -- small balance write-off

    IF @Total < 0
    BEGIN
        INSERT INTO dbo.RefundQueue (AccountId, Amount, QueuedOn) VALUES (@AccountId, -@Total, GETDATE());
    END
    ELSE IF @Total > 0
    BEGIN
        INSERT INTO dbo.Charge (AccountId, ChargeType, Amount, CreatedOn) VALUES (@AccountId, 'FINAL', @Total, GETDATE());
    END

    UPDATE dbo.Account SET Status = 'F', FinalBilledOn = GETDATE() WHERE AccountId = @AccountId;

    SELECT @BaseCharge AS BaseCharge, @UsageCharge AS UsageCharge, @Total AS Total;
END
