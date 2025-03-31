-- דוגמה לפונקציה טבלאית הפועלת לפי סטנדרט שמות השדות החדש
CREATE FUNCTION [dbo].[GetArnReport] (
    @FromDate DATE = NULL,
    @ToDate DATE = NULL
)
RETURNS TABLE
AS
RETURN
(
    SELECT 
        arn.hs AS ARN_HS,               -- שדה מטבלה (מיפוי לפי ARN + HS)
        arn.mspkod AS ARN_MSPKOD,       -- שדה מטבלה (מיפוי לפי ARN + MSPKOD)
        hs.isvkod AS HS_ISVKOD,         -- שדה מטבלה (מיפוי לפי HS + ISVKOD)
        
        -- שדות מחושבים (ללא "_")
        (arn.paysum - arn.sumhan - arn.sumhk) AS bruto,  -- שדה מחושב (מיפוי לפי GetArnReport + bruto)
        CAST((12.0 / dbo.ClosedPeriod()) * 
            (arn.paysum - arn.sumhan - arn.sumhk) AS DECIMAL(18,2)) 
            AS allbruto,                 -- שדה מחושב (מיפוי לפי GetArnReport + allbruto)
            
        CASE WHEN arn.activ = 1 THEN 'פעיל' ELSE 'לא פעיל' END AS status  -- שדה מחושב נוסף
            
    FROM dbo.arn 
    JOIN dbo.hs ON arn.hs = hs.hskod
    WHERE (@FromDate IS NULL OR arn.date >= @FromDate)
      AND (@ToDate IS NULL OR arn.date <= @ToDate)
)
GO

-- דוגמה למיפויים לפי הסטנדרט החדש
INSERT INTO ReportsGeneratorColumns (TableName, ColumnName, HebrewAlias) VALUES
-- שדות מטבלה (יופיעו בשאילתות בפורמט TableName_ColumnName)
('ARN', 'HS', N'מספר השגחה'),
('ARN', 'MSPKOD', N'קוד מוסד'),
('HS', 'ISVKOD', N'קוד ישוב'),

-- שדות מחושבים (יופיעו בשאילתות ללא "_")
('GetArnReport', 'bruto', N'סכום ברוטו'),
('GetArnReport', 'allbruto', N'ברוטו שנתי'),
('GetArnReport', 'status', N'סטטוס פעילות')
GO