namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// קודי שגיאה במערכת
    /// </summary>
    public enum ErrorCode
    {
        // שגיאות כלליות
        General_Info,
        General_Warning,
        General_Error,
        
        // שגיאות בסיס נתונים
        DB_Connection_Failed,
        DB_Query_Failed,
        DB_Report_NotFound,
        DB_Report_Config_Invalid,
        DB_TableFunc_Execution_Failed,
        DB_StoredProc_Execution_Failed,
        DB_StoredProc_MissingParam,
        DB_MonthName_NotFound,
        DB_IshvName_NotFound,
        DB_SugtsName_NotFound,
        DB_MoazaName_NotFound,
        DB_ColumnMapping_NotFound,
        
        // שגיאות פרמטרים
        Parameters_Missing,
        Parameters_Invalid_Type,
        Parameters_Invalid_Value,
        
        // שגיאות תבנית
        Template_Not_Found,
        Template_Invalid_Format,
        Template_Processing_Failed,
        Template_Missing_Placeholder,
        Template_Condition_Invalid,
        Template_Table_Row_Missing,
        Template_Invalid_Department,
        Template_Reading_Failed,
        
        // שגיאות יצירת PDF
        PDF_Generation_Failed,
        PDF_Chrome_Not_Found,
        PDF_Generation_Success,
        
        // שגיאות יצירת Excel
        Excel_Generation_Failed,
        Excel_Generation_Success,
        
        // שגיאות יצירת דוח
        Report_Generation_Failed,
        Report_Execution_Failed,
        PDF_Html_Conversion_Failed,
        Parameters_Invalid,
        Parameters_Type_Mismatch,
        Report_Data_Retrieval_Failed,
        Report_Save_Failed
    }

    /// <summary>
    /// מקשר קודי שגיאה למחרוזות
    /// </summary>
    public static class ErrorCodeMapper
    {
        /// <summary>
        /// קבלת מחרוזת קוד שגיאה
        /// </summary>
        public static string GetErrorCodeString(ErrorCode code)
        {
            return code switch
            {
                // שגיאות כלליות
                ErrorCode.General_Info => "INFO001",
                ErrorCode.General_Warning => "WARN001",
                ErrorCode.General_Error => "ERR001",
                
                // שגיאות בסיס נתונים
                ErrorCode.DB_Connection_Failed => "DB001",
                ErrorCode.DB_Query_Failed => "DB002",
                ErrorCode.DB_Report_NotFound => "DB003",
                ErrorCode.DB_Report_Config_Invalid => "DB004",
                ErrorCode.DB_TableFunc_Execution_Failed => "DB005",
                ErrorCode.DB_StoredProc_Execution_Failed => "DB006",
                ErrorCode.DB_StoredProc_MissingParam => "DB007",
                ErrorCode.DB_MonthName_NotFound => "DB008",
                ErrorCode.DB_IshvName_NotFound => "DB009",
                ErrorCode.DB_SugtsName_NotFound => "DB010",
                ErrorCode.DB_MoazaName_NotFound => "DB011",
                ErrorCode.DB_ColumnMapping_NotFound => "DB012",
                
                // שגיאות פרמטרים
                ErrorCode.Parameters_Missing => "PARAM001",
                ErrorCode.Parameters_Invalid_Type => "PARAM002",
                ErrorCode.Parameters_Invalid_Value => "PARAM003",
                
                // שגיאות תבנית
                ErrorCode.Template_Not_Found => "TMPL001",
                ErrorCode.Template_Invalid_Format => "TMPL002",
                ErrorCode.Template_Processing_Failed => "TMPL003",
                ErrorCode.Template_Missing_Placeholder => "TMPL004",
                ErrorCode.Template_Condition_Invalid => "TMPL005",
                ErrorCode.Template_Table_Row_Missing => "TMPL006",
                ErrorCode.Template_Invalid_Department => "TMPL007",
                ErrorCode.Template_Reading_Failed => "TMPL008",
                
                // שגיאות יצירת PDF
                ErrorCode.PDF_Generation_Failed => "PDF001",
                ErrorCode.PDF_Chrome_Not_Found => "PDF002",
                ErrorCode.PDF_Generation_Success => "PDF003",
                
                // שגיאות יצירת Excel
                ErrorCode.Excel_Generation_Failed => "XLS001",
                ErrorCode.Excel_Generation_Success => "XLS002",
                
                // שגיאות יצירת דוח
                ErrorCode.Report_Generation_Failed => "RPT001",
                ErrorCode.Report_Execution_Failed => "RPT002",
                
                _ => "UNKNOWN"
            };
        }
    }
}
