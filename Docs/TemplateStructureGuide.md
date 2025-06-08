# מדריך מבנה תבניות HTML למערכת הדוחות

## סקירה כללית

תבניות HTML במערכת הדוחות משתמשות במנוע Handlebars לעיבוד דינמי. יש שתי גישות מרכזיות לעבודה עם נתונים:

### גישה 1: Handlebars מלא (מומלץ לתבניות חדשות)
- שימוש ב-`{{#each}}` לטבלאות עם מספר שורות
- שימוש ב-`{{#with}}` לנתונים בודדים
- תמיכה מלאה בתנאים והיררכיה

### גישה 2: data-table-row (תאימות אחורה)
- שימוש ב-`data-table-row` על אלמנטים
- עבודה ישירה עם placeholders

## מבנה בסיסי של תבנית

```html
<!DOCTYPE html>
<html lang="he-IL" dir="rtl">
<head>
    <meta charset="UTF-8">
    <title>{{ReportTitle}}</title>
    <style>
        /* עיצוב CSS */
    </style>
</head>
<body>
    <!-- תוכן הדוח -->
</body>
</html>
```

## סוגי Placeholders

### 1. פלייסהולדרים פשוטים
```html
{{ReportTitle}}         <!-- כותרת הדוח -->
{{CurrentDate}}         <!-- תאריך נוכחי -->
{{CurrentTime}}         <!-- שעה נוכחית -->
{{mnt}}                 <!-- פרמטר שהועבר -->
{{PageNumber}}          <!-- מספר עמוד -->
{{TotalPages}}          <!-- סך עמודים -->
```

### 2. פלייסהולדרים עם פורמט
```html
{{format sumAmount}}    <!-- פורמט מספרי עם פסיקים -->
```

### 3. כותרות עמודות
```html
{{HEADER:ColumnName}}   <!-- תרגום לעברית של שם עמודה -->
{{header 'ColumnName'}} <!-- תחביר חלופי -->
```

## עבודה עם נתונים

### 1. נתונים בודדים (שורה אחת)

**גישת Handlebars (מומלץ):**
```html
{{#with GetArnSummaryPeriodic}}
<div>
    <p>סכום: {{format bruto}}</p>
    <p>מספר נכסים: {{cnths}}</p>
</div>
{{/with}}
```

**גישת data-table-row:**
```html
<div data-table-row="GetArnSummaryPeriodic">
    <p>סכום: {{bruto}}</p>
    <p>מספר נכסים: {{cnths}}</p>
</div>
```

### 2. טבלאות עם מספר שורות

**גישת Handlebars (מומלץ):**
```html
<table>
    <thead>
        <tr>
            <th>{{HEADER:hesder}}</th>
            <th>{{HEADER:paysum}}</th>
        </tr>
    </thead>
    <tbody>
        {{#each GetArnPaymentMethodSummary_list}}
        <tr>
            <td>{{hesder}}</td>
            <td>{{format paysum}}</td>
        </tr>
        {{/each}}
    </tbody>
</table>
```

**גישת data-table-row:**
```html
<tr data-table-row="GetArnPaymentMethodSummary">
    <td>{{hesder}}</td>
    <td>{{paysum}}</td>
</tr>
```

## תנאים בתבניות

### 1. תנאי פשוט
```html
{{#if fieldName}}
    <!-- מוצג אם fieldName קיים ולא ריק -->
{{/if}}
```

### 2. השוואת ערכים
```html
{{#if hesder == -1}}
    <td>סה"כ</td>
{{else}}
    <td>{{hesder}}</td>
{{/if}}
```

### 3. שימוש ב-Helper מותאם אישית
```html
{{#isSummary IsSummary}}
    <!-- זו שורת סיכום -->
{{else}}
    <!-- שורה רגילה -->
{{/isSummary}}
```

### 4. תנאים מורכבים
```html
{{#if IsSummary == 1}}
    <td class="summary">סה"כ</td>
{{else}}
    <td>{{description}}</td>
{{/if}}
```

## מבנה מומלץ לדוח מלא

```html
<!DOCTYPE html>
<html lang="he-IL" dir="rtl">
<head>
    <meta charset="UTF-8">
    <title>{{ReportTitle}}</title>
    <style>
        /* עיצוב בסיסי */
        body { 
            font-family: Arial, sans-serif; 
            direction: rtl; 
        }
        .report-header { 
            text-align: center; 
            margin-bottom: 20px; 
        }
        .data-table { 
            width: 100%; 
            border-collapse: collapse; 
        }
        .data-table th, .data-table td { 
            border: 1px solid #ddd; 
            padding: 8px; 
        }
        /* עיצוב לשורות סיכום */
        .summary-row { 
            font-weight: bold; 
            background-color: #f2f2f2; 
        }
    </style>
</head>
<body>
    <!-- כותרת -->
    <div class="report-header">
        <h1>{{ReportTitle}}</h1>
        <p>{{CurrentDate}}</p>
    </div>

    <!-- פרמטרים -->
    <div class="params">
        <p>חודש: {{mntname}}</p>
        <p>ישוב: {{ishvname}}</p>
    </div>

    <!-- נתונים בודדים -->
    {{#with GetSummaryData}}
    <div class="summary">
        <h2>סיכום כללי</h2>
        <p>סה"כ: {{format totalAmount}}</p>
        <p>מספר רשומות: {{count}}</p>
    </div>
    {{/with}}

    <!-- טבלת נתונים -->
    <h2>פירוט</h2>
    <table class="data-table">
        <thead>
            <tr>
                <th>{{HEADER:code}}</th>
                <th>{{HEADER:description}}</th>
                <th>{{HEADER:amount}}</th>
            </tr>
        </thead>
        <tbody>
            {{#each GetDetailData_list}}
            <tr>
                {{#if IsSummary == 1}}
                    <td class="summary-row" colspan="2">סה"כ</td>
                    <td class="summary-row">{{format amount}}</td>
                {{else}}
                    <td>{{code}}</td>
                    <td>{{description}}</td>
                    <td>{{format amount}}</td>
                {{/if}}
            </tr>
            {{/each}}
        </tbody>
    </table>
</body>
</html>
```

## כללי שמות חשובים

### 1. שמות פרוצדורות/פונקציות
- לנתון בודד: `GetArnSummaryPeriodic`
- לרשימה: `GetArnPaymentMethodSummary` (המערכת תוסיף `_list` אוטומטית)

### 2. גישה לנתונים
- נתון בודד: `{{#with ProcedureName}}`
- רשימה: `{{#each ProcedureName_list}}`

### 3. שדות מיוחדים לזיהוי שורות סיכום
- `IsSummary` - ערך 1 או true לשורת סיכום
- `hesder` - ערך -1 לשורת סיכום
- ניתן להגדיר שדות נוספים בפרוצדורה

## טיפים לעיצוב

### 1. עיצוב מותנה
```css
/* שימוש בclass מותנה */
.summary-row { 
    background-color: #f0f8ff; 
    font-weight: bold; 
}
```

### 2. עיצוב מספרים
```css
.amount { 
    text-align: left; 
    direction: ltr; 
}
```

### 3. הדפסה
```css
@media print {
    .no-print { display: none; }
    .page-break { page-break-after: always; }
}
```

## דוגמאות לתבניות נפוצות

### 1. דוח עם סיכומים מרובים
```html
<div class="grid-container">
    {{#with GetSummary1}}
    <div class="summary-box">
        <!-- תוכן סיכום 1 -->
    </div>
    {{/with}}
    
    {{#with GetSummary2}}
    <div class="summary-box">
        <!-- תוכן סיכום 2 -->
    </div>
    {{/with}}
</div>
```

### 2. טבלה עם קיבוץ
```html
{{#each GetGroupedData_list}}
    {{#if IsGroupHeader}}
        <tr class="group-header">
            <td colspan="3">{{GroupName}}</td>
        </tr>
    {{else}}
        <tr>
            <td>{{field1}}</td>
            <td>{{field2}}</td>
            <td>{{field3}}</td>
        </tr>
    {{/if}}
{{/each}}
```

### 3. תמיכה בכמה פרוצדורות
```html
<!-- חלק 1 -->
{{#each Procedure1_list}}
    <!-- תוכן -->
{{/each}}

<!-- חלק 2 -->
{{#each Procedure2_list}}
    <!-- תוכן -->
{{/each}}
```

## פתרון בעיות נפוצות

### 1. נתונים לא מופיעים
- בדוק ששם הפרוצדורה נכון
- ודא שיש `_list` לרשימות
- בדוק שהשדה קיים בתוצאות

### 2. תנאים לא עובדים
- השתמש ב-`==` (כפול) להשוואה
- ודא שהערך מהסוג הנכון
- נסה helpers מותאמים כמו `{{#isSummary}}`

### 3. עיצוב לא מוחל
- השתמש ב-`!important` בCSS אם צריך
- בדוק specificity של הסלקטורים

## המלצות

1. **התחל מתבנית קיימת** - קח תבנית דומה ושנה אותה
2. **בדוק בשלבים** - הוסף חלקים בהדרגה
3. **השתמש בHandlebars** - העדף את הגישה החדשה
4. **תעד את השדות** - הוסף הערות על השדות הנדרשים
5. **בדוק עם נתוני אמת** - תמיד בדוק עם נתונים אמיתיים

## סיכום

המערכת תומכת בשתי גישות - הישנה עם `data-table-row` והחדשה עם Handlebars מלא. 
לתבניות חדשות מומלץ להשתמש בגישת Handlebars שנותנת יותר גמישות ויכולות.
