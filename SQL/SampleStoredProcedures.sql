-- דוגמה לפרוצדורה לשליפת לקוחות
CREATE PROCEDURE GetCustomers
    @DateFrom DATE = NULL,
    @DateTo DATE = NULL
AS
BEGIN
    SELECT 
        C.CUSTOMER_ID,
        C.CUSTOMER_NAME,
        C.ADDRESS,
        C.PHONE,
        C.EMAIL,
        (
            SELECT SUM(O.AMOUNT - ISNULL(P.PAID_AMOUNT, 0))
            FROM ORDERS O
            LEFT JOIN PAYMENTS P ON O.ORDER_ID = P.ORDER_ID
            WHERE O.CUSTOMER_ID = C.CUSTOMER_ID
        ) AS BALANCE,
        (
            SELECT MAX(ORDER_DATE)
            FROM ORDERS
            WHERE CUSTOMER_ID = C.CUSTOMER_ID
        ) AS LAST_ORDER_DATE
    FROM 
        CUSTOMERS C
    WHERE 
        EXISTS (
            SELECT 1 FROM ORDERS O
            WHERE O.CUSTOMER_ID = C.CUSTOMER_ID
            AND (@DateFrom IS NULL OR O.ORDER_DATE >= @DateFrom)
            AND (@DateTo IS NULL OR O.ORDER_DATE <= @DateTo)
        )
    ORDER BY
        C.CUSTOMER_NAME
END
GO

-- דוגמה לפרוצדורה לשליפת יתרות לקוחות
CREATE PROCEDURE GetCustomerBalances
    @MinBalance DECIMAL = 0
AS
BEGIN
    SELECT 
        C.CUSTOMER_ID,
        C.CUSTOMER_NAME,
        (
            SELECT SUM(O.AMOUNT - ISNULL(P.PAID_AMOUNT, 0))
            FROM ORDERS O
            LEFT JOIN PAYMENTS P ON O.ORDER_ID = P.ORDER_ID
            WHERE O.CUSTOMER_ID = C.CUSTOMER_ID
        ) AS BALANCE,
        C.PHONE,
        C.EMAIL
    FROM 
        CUSTOMERS C
    WHERE 
        (
            SELECT SUM(O.AMOUNT - ISNULL(P.PAID_AMOUNT, 0))
            FROM ORDERS O
            LEFT JOIN PAYMENTS P ON O.ORDER_ID = P.ORDER_ID
            WHERE O.CUSTOMER_ID = C.CUSTOMER_ID
        ) > @MinBalance
    ORDER BY
        BALANCE DESC
END
GO

-- דוגמה ליצירת טבלאות לבדיקה
-- טבלת לקוחות
CREATE TABLE CUSTOMERS (
    CUSTOMER_ID INT PRIMARY KEY,
    CUSTOMER_NAME NVARCHAR(100) NOT NULL,
    ADDRESS NVARCHAR(200),
    PHONE NVARCHAR(20),
    EMAIL NVARCHAR(100)
);

-- טבלת הזמנות
CREATE TABLE ORDERS (
    ORDER_ID INT PRIMARY KEY,
    CUSTOMER_ID INT FOREIGN KEY REFERENCES CUSTOMERS(CUSTOMER_ID),
    ORDER_DATE DATE,
    AMOUNT DECIMAL(18,2),
    STATUS NVARCHAR(20)
);

-- טבלת תשלומים
CREATE TABLE PAYMENTS (
    PAYMENT_ID INT PRIMARY KEY,
    ORDER_ID INT FOREIGN KEY REFERENCES ORDERS(ORDER_ID),
    PAYMENT_DATE DATE,
    PAID_AMOUNT DECIMAL(18,2)
);

-- הכנסת נתוני דוגמה
INSERT INTO CUSTOMERS (CUSTOMER_ID, CUSTOMER_NAME, ADDRESS, PHONE, EMAIL) VALUES
(1, N'ישראל ישראלי', N'רחוב הרצל 1, תל אביב', '050-1234567', 'israel@example.com'),
(2, N'שרה כהן', N'שדרות רוטשילד 10, תל אביב', '052-7654321', 'sara@example.com'),
(3, N'משה לוי', N'רחוב יפו 5, ירושלים', '054-1112233', 'moshe@example.com');

INSERT INTO ORDERS (ORDER_ID, CUSTOMER_ID, ORDER_DATE, AMOUNT, STATUS) VALUES
(1, 1, '2023-01-15', 1200.50, N'הושלם'),
(2, 1, '2023-02-20', 450.75, N'הושלם'),
(3, 2, '2023-01-10', 800.25, N'הושלם'),
(4, 2, '2023-03-05', 1500.00, N'בטיפול'),
(5, 3, '2023-02-28', 350.50, N'בטיפול');

INSERT INTO PAYMENTS (PAYMENT_ID, ORDER_ID, PAYMENT_DATE, PAID_AMOUNT) VALUES
(1, 1, '2023-01-20', 1200.50),
(2, 2, '2023-02-25', 200.00),
(3, 3, '2023-01-15', 800.25);
