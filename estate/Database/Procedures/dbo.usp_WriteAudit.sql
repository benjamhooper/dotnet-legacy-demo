CREATE PROCEDURE dbo.usp_WriteAudit
    @Area VARCHAR(30), @Message NVARCHAR(MAX), @User VARCHAR(50), @On DATETIME
AS
    INSERT INTO dbo.Audit (Area, Message, UserName, LoggedOn) VALUES (@Area, @Message, @User, @On);
