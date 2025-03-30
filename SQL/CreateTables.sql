-- יצירת טבלת הגדרות דוחות
CREATE TABLE ReportsGenerator (
    ReportID INT IDENTITY(1,1) PRIMARY KEY,
    ReportName NVARCHAR(100) NOT NULL,   -- שם הדוח (גם שם קובץ התבנית)
    StoredProcName NVARCHAR(250) NOT NULL, -- שמות הפרוצדורות (מופרדות ב-;)
    Title NVARCHAR(200) NOT NULL,        -- כותרת הדוח
    Description NVARCHAR(500) NULL       -- תיאור הדוח
);

-- יצירת טבלת מיפויי כותרות
CREATE TABLE ReportsGeneratorColumns (
    TableName NVARCHAR(100) NOT NULL,      -- שם טבלה לוגי
    ColumnName NVARCHAR(100) NOT NULL,     -- שם עמודה
    HebrewAlias NVARCHAR(100) NOT NULL,    -- כותרת בעברית
    SpecificProcName NVARCHAR(100) NULL,   -- פרוצדורה ספציפית (אופציונלי)
    SpecificAlias NVARCHAR(100) NULL,      -- כותרת ספציפית לפרוצדורה
    PRIMARY KEY (TableName, ColumnName, ISNULL(SpecificProcName, ''))
);

-- דוגמאות למיפויי כותרות בסיסיים
INSERT INTO ReportsGeneratorColumns (TableName, ColumnName, HebrewAlias) VALUES
('CUSTOMERS', 'CUSTOMER_ID', N'מספר לקוח'),
('CUSTOMERS', 'CUSTOMER_NAME', N'שם לקוח'),
('CUSTOMERS', 'ADDRESS', N'כתובת'),
('CUSTOMERS', 'PHONE', N'טלפון'),
('CUSTOMERS', 'EMAIL', N'דוא"ל'),
('CUSTOMERS', 'BALANCE', N'יתרה'),
('CUSTOMERS', 'LAST_ORDER_DATE', N'תאריך הזמנה אחרון'),
('ORDERS', 'ORDER_ID', N'מספר הזמנה'),
('ORDERS', 'ORDER_DATE', N'תאריך הזמנה'),
('ORDERS', 'AMOUNT', N'סכום'),
('ORDERS', 'STATUS', N'סטטוס');

-- דוגמה למיפוי ספציפי לפרוצדורה
INSERT INTO ReportsGeneratorColumns (TableName, ColumnName, HebrewAlias, SpecificProcName, SpecificAlias) VALUES
('CUSTOMERS', 'BALANCE', N'יתרה', 'GetCustomerBalances', N'יתרת חוב');

-- דוגמה להגדרת דוח
INSERT INTO ReportsGenerator (ReportName, StoredProcName, Title, Description) VALUES
('CustomerReport', 'GetCustomers', N'דוח לקוחות', N'דוח המציג את כל הלקוחות במערכת'),
('BalanceReport', 'GetCustomerBalances', N'דוח יתרות לקוחות', N'דוח המציג את היתרות של כל הלקוחות');
