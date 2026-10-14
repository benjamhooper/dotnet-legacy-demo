-- Who gets a truck roll first. Nobody in Collections has seen this code.
CREATE PROCEDURE dbo.usp_AssessCollectionsPriority
    @AsOf DATE
AS
BEGIN
    SELECT a.AccountId,
           CASE
             WHEN a.HardshipProgram = 1 THEN 0
             WHEN MONTH(@AsOf) IN (11,12,1,2,3) THEN 0                      -- winter moratorium
             WHEN a.PastDueAmount > 1000 AND a.RateClassId = 2 THEN 100
             WHEN a.PastDueAmount > 500 THEN 80
             WHEN a.LateCount12Months >= 3 THEN 60
             WHEN DATEDIFF(DAY, a.DueDate, @AsOf) > 60 THEN 40
             ELSE 10
           END AS Priority
      FROM dbo.Account a
     WHERE a.PastDueAmount > 0 AND a.Status = 'A'
     ORDER BY Priority DESC, a.PastDueAmount DESC;
END
