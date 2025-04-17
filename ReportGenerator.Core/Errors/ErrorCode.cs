namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// קודי שגיאה סטנדרטיים למערכת
    /// </summary>
    public enum ErrorCode
    {
        // שגיאות כלליות
        General_Error,
        
        // שגיאות מסד נתונים
        DB_Connection_Failed,
        DB_Query_Failed,
        DB_Report_NotFound,
        DB_Report_Config_Invalid,
        DB_ColumnMapping_NotFound,
        DB_MonthName_NotFound,
        DB_SugtsName_NotFound,
        DB_IshvName_NotFound,
        DB_MoazaName_NotFound,
        DB_StoredProc_MissingParam,
        DB_StoredProc_Execution_Failed,
        DB_TableFunc_Execution_Failed,
        
        // שגיאות פרמטרים
        Parameters_Invalid,
        Parameters_Type_Mismatch,
        Parameters_Missing,
        
        // שגיאות תבנית
        Template_Not_Found,
        Template_Invalid_Format,
        Template_Processing_Failed,
        Template_Table_Row_Missing,
        Template_Table_Row_Invalid,
        Template_Missing_Placeholder,
        Template_Condition_Invalid,
        
        // שגיאות PDF
        PDF_Generation_Failed,
        PDF_Chrome_Not_Found,
        PDF_Html_Conversion_Failed,
        
        // שגיאות Excel
        Excel_Generation_Failed,
        
        // שגיאות דוח
        Report_Generation_Failed
    }
}
