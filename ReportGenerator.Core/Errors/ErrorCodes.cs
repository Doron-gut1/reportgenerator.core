using System;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// קודי שגיאה קבועים למערכת - מאפשר עקביות בקודי השגיאה
    /// </summary>
    public static class ErrorCodes
    {
        // קודי שגיאה של גישה לנתונים (DB)
        public static class DB
        {
            // שגיאות כלליות
            public const string Connection_Failed = "DB_Connection_Failed";
            public const string Query_Failed = "DB_Query_Failed";
            
            // שגיאות דוחות
            public const string Report_NotFound = "DB_Report_NotFound";
            public const string Report_Config_Invalid = "DB_Report_Config_Invalid";
            
            // שגיאות פרוצדורות מאוחסנות
            public const string StoredProc_NotFound = "DB_StoredProc_NotFound";
            public const string StoredProc_MissingParam = "DB_StoredProc_MissingParam";
            public const string StoredProc_Execution_Failed = "DB_StoredProc_Execution_Failed";
            
            // שגיאות טבלאות
            public const string Table_NotFound = "DB_Table_NotFound";
            
            // שגיאות מיפוי עמודות
            public const string ColumnMapping_NotFound = "DB_ColumnMapping_NotFound";
            
            // שגיאות פונקציות טבלאיות
            public const string TableFunc_NotFound = "DB_TableFunc_NotFound";
            public const string TableFunc_Execution_Failed = "DB_TableFunc_Execution_Failed";
            
            // שגיאות נתונים
            public const string SugtsName_NotFound = "DB_SugtsName_NotFound";
            public const string MonthName_NotFound = "DB_MonthName_NotFound";
            public const string IshvName_NotFound = "DB_IshvName_NotFound";

            public static string MoazaName_NotFound { get; internal set; }
        }
        
        // קודי שגיאה של תבניות HTML
        public static class Template
        {
            // שגיאות כלליות
            public const string Not_Found = "Template_Not_Found";
            public const string Invalid_Format = "Template_Invalid_Format";
            
            // שגיאות עיבוד תבנית
            public const string Processing_Failed = "Template_Processing_Failed";
            public const string Missing_Placeholder = "Template_Missing_Placeholder";
            public const string Invalid_Placeholder = "Template_Invalid_Placeholder";
            
            // שגיאות טבלאות דינמיות
            public const string Table_Row_Missing = "Template_Table_Row_Missing";
            public const string Table_Row_Invalid = "Template_Table_Row_Invalid";
            
            // שגיאות תנאים
            public const string Condition_Invalid = "Template_Condition_Invalid";
        }
        
        // קודי שגיאה של מחולל PDF
        public static class PDF
        {
            // שגיאות כלליות
            public const string Generation_Failed = "PDF_Generation_Failed";
            
            // שגיאות HTML-to-PDF
            public const string Html_Conversion_Failed = "PDF_Html_Conversion_Failed";
            public const string Chrome_Not_Found = "PDF_Chrome_Not_Found";
            
            // שגיאות עיצוב PDF
            public const string Style_Invalid = "PDF_Style_Invalid";
        }
        
        // קודי שגיאה של מחולל Excel
        public static class Excel
        {
            // שגיאות כלליות
            public const string Generation_Failed = "Excel_Generation_Failed";
            
            // שגיאות נתונים
            public const string Data_Format_Invalid = "Excel_Data_Format_Invalid";
            public const string Column_Mapping_Failed = "Excel_Column_Mapping_Failed";
            
            // שגיאות עיצוב
            public const string Style_Invalid = "Excel_Style_Invalid";
        }
        
        // קודי שגיאה של מנהל הדוחות
        public static class Report
        {
            // שגיאות כלליות
            public const string Generation_Failed = "Report_Generation_Failed";
            
            // שגיאות פרמטרים
            public const string Parameters_Invalid = "Report_Parameters_Invalid";
            public const string Parameters_Missing = "Report_Parameters_Missing";
            public const string Parameters_Type_Mismatch = "Report_Parameters_Type_Mismatch";
        }
    }
}
