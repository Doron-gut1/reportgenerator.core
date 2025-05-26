# RunApiAndSaveReport - מערכת לקוח לגנרציית דוחות

כלי command-line להפקת דוחות מהשרת ושמירתם במקומי.

## תכונות עיקריות

- תמיכה בהפקת דוחות PDF ו-Excel
- עבודה עם פרמטרים דינמיים
- שמירה מקומית של הדוחות
- מצב אינטראקטיבי ומצב command-line
- טיפול בשגיאות ותיעוד מלא

## דרישות מערכת

- .NET Framework 4.8
- גישה לשרת API (מוגדר ב-App.config)
- הרשאות כתיבה לתיקיית הפלט

## הגדרות (App.config)

```xml
<appSettings>
    <!-- הגדרות API -->
    <add key="ApiBaseUrl" value="http://localhost:5000/api/reports" />
    <add key="ApiTimeout" value="300" /><!-- 5 דקות -->
    
    <!-- הגדרות שמירה -->
    <add key="DefaultOutputFolder" value="C:\Reports" />
    <add key="CreateFolderIfNotExists" value="true" />
    
    <!-- הגדרות כלליות -->
    <add key="ShowDetailedErrors" value="true" />
    <add key="LogToConsole" value="true" />
</appSettings>
```

## שימוש - Command Line

### תחביר בסיסי:
```
RunApiAndSaveReport.exe <ReportName> <OutputFormat> <OutputPath> [Parameters]
```

### דוגמאות:

```bash
# דוח PDF פשוט
RunApiAndSaveReport.exe ArnSummaryReport PDF C:\Reports

# דוח Excel עם פרמטרים
RunApiAndSaveReport.exe ArnSummaryReport Excel C:\Reports mnt=3,isvkod=1

# דוח עם פרמטרים מורכבים
RunApiAndSaveReport.exe TrfbysugtsSummaryReport PDF C:\Reports startDate=2024-01-01,endDate=2024-12-31,active=true
```

## שימוש - מצב אינטראקטיבי

הרצת התוכנה ללא פרמטרים תפעיל מצב אינטראקטיבי:

```bash
RunApiAndSaveReport.exe
```

המערכת תבקש:
1. שם הדוח
2. פורמט הפלט (PDF/Excel)
3. נתיב שמירה
4. פרמטרים (אופציונלי)

## פורמט פרמטרים

פרמטרים מועברים בפורמט: `key1=value1,key2=value2`

המערכת מזהה אוטומטית סוגי נתונים:
- מספרים שלמים (Int32)
- תאריכים (DateTime)
- בוליאנים (Boolean)
- מחרוזות (String) - ברירת מחדל

### דוגמאות לפרמטרים:
- `mnt=3` → מספר שלם
- `startDate=2024-01-01` → תאריך
- `active=true` → בוליאן
- `customerName=Israel` → מחרוזת

## קודי חזרה

- `0` - הצלחה
- `1` - שגיאה (פרטים בלוג)

## בעיות נפוצות

### "שגיאת רשת"
- בדוק שהשרת פועל
- בדוק שה-URL נכון ב-App.config
- בדוק חיבור לרשת

### "שגיאה בשמירת קובץ"
- בדוק הרשאות כתיבה לתיקיית הפלט
- בדוק שיש מספיק מקום בדיסק
- בדוק שהתיקייה קיימת (או הפעל CreateFolderIfNotExists)

### "שגיאת timeout"
- הגדל את ApiTimeout ב-App.config
- בדוק שהדוח לא כבד מדי

## פיתוח ותחזוקה

### מבנה הקוד:
- `Program.cs` - הקוד הראשי
- `App.config` - הגדרות המערכת
- `packages.config` - תלות בספריות חיצוניות

### תלות בספריות:
- `Newtonsoft.Json` - לטיפול ב-JSON
- `System.Configuration` - לקריאת App.config
- `System.Net.Http` - לקריאות API

## דוגמאות שימוש מתקדמות

### הפעלה מ-VBA (Access):
```vb
Dim command As String
command = "RunApiAndSaveReport.exe ArnSummaryReport PDF C:\Reports mnt=3"
Shell command, vbNormalFocus
```

### הפעלה מ-Batch:
```batch
@echo off
cd /d "C:\Program Files\ReportGenerator"
RunApiAndSaveReport.exe %1 %2 %3 %4
if %errorlevel% neq 0 (
    echo Error generating report
    pause
)
```

---
**גרסה:** 1.0  
**תאריך עדכון אחרון:** $CURRENT_DATE$
