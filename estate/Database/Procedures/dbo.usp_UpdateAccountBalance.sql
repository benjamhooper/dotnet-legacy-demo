CREATE PROCEDURE dbo.usp_UpdateAccountBalance
    @AccountId INT, @Balance DECIMAL(10,2), @ModifiedBy VARCHAR(50), @ModifiedOn DATETIME
AS
    UPDATE dbo.Account SET CurrentBalance = @Balance, ModifiedBy = @ModifiedBy, ModifiedOn = @ModifiedOn
     WHERE AccountId = @AccountId;
