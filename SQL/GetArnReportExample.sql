-- דוגמה למיפוי כותרות עבור פונקציית GetArnReport
-- הפונקציה מחזירה נתוני השגחה לפי חודש וקוד ישוב (אופציונלי)

-- 1. הגדרת הדוח במערכת
INSERT INTO ReportsGenerator (ReportName, StoredProcName, Title, Description) VALUES
('ArnMonthlyReport', 'dbo.GetArnReport', N'דוח חודשי השגחות', N'דוח המציג את נתוני ההשגחות החודשיים');

-- 2. מיפוי כותרות עמודות
-- שדות מטבלה (עם "_")
INSERT INTO ReportsGeneratorColumns (TableName, ColumnName, HebrewAlias) VALUES
('ARN', 'HS', N'מספר השגחה'),
('ARN', 'MSPKOD', N'קוד מוסד'),
('ARN', 'GDL2PAY', N'גודל לתשלום'),
('ARN', 'HNCKOD', N'קוד הנחה'),
('ARN', 'PAYSUM', N'סכום לתשלום'),
('ARN', 'SUMHK', N'סכום ה"ק'),
('ARN', 'SUMHAN', N'סכום הנחה'),
('ARN', 'HKARN', N'ה"ק ארנונה'),
('ARN', 'MNT', N'חודש'),
('HS', 'ISVKOD', N'קוד ישוב');

-- שדות מחושבים (ללא "_")
INSERT INTO ReportsGeneratorColumns (TableName, ColumnName, HebrewAlias) VALUES
('GetArnReport', 'bruto', N'ברוטו חודשי'),
('GetArnReport', 'allbruto', N'ברוטו שנתי'),
('GetArnReport', 'adv', N'שולם מראש'),
('GetArnReport', 'activ', N'פעיל'),
('GetArnReport', 'hesder', N'הסדר');

-- 3. יצירת תבנית HTML 'ArnMonthlyReport.html' בתיקיית התבניות

-- 4. קריאה לדוח מ-Access:
/*
Dim reportDLL As New ReportGenerator
Dim params As Object
params = Array( _
    "mnt", 3, 8, _  ' חודש מרץ (DbType.Int32)
    "isvkod", 1, 8 _  ' קוד ישוב (DbType.Int32)
)

Dim result = reportDLL.GenerateReport("ArnMonthlyReport", OutputFormat.PDF, params)
*/