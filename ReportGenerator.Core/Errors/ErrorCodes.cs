using System;

namespace ReportGenerator.Core.Errors
{
    /// <summary>
    /// קודי שגיאה של המערכת
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// קודי שגיאה לתבניות
        /// </summary>
        public static class Template
        {
            public static readonly ErrorCode Not_Found = ErrorCode.Template_Not_Found;
            public static readonly ErrorCode Invalid_Format = ErrorCode.Template_Invalid_Format;
            public static readonly ErrorCode Processing_Failed = ErrorCode.Template_Processing_Failed;
            public static readonly ErrorCode Condition_Invalid = ErrorCode.Template_Condition_Invalid;
            public static readonly ErrorCode Table_Row_Missing = ErrorCode.Template_Table_Row_Missing;
            public static readonly ErrorCode Table_Row_Invalid = ErrorCode.Template_Table_Row_Invalid;
            public static readonly ErrorCode Missing_Placeholder = ErrorCode.Template_Missing_Placeholder;
        }

        /// <summary>
        /// קודי שגיאה ל-PDF
        /// </summary>
        public static class PDF
        {
            public static readonly ErrorCode Generation_Failed = ErrorCode.PDF_Generation_Failed;
            public static readonly ErrorCode Chrome_Not_Found = ErrorCode.PDF_Chrome_Not_Found;
            public static readonly ErrorCode Html_Conversion_Failed = ErrorCode.PDF_Html_Conversion_Failed;
        }

        /// <summary>
        /// קודי שגיאה לExcel
        /// </summary>
        public static class Excel
        {
            public static readonly ErrorCode Generation_Failed = ErrorCode.Excel_Generation_Failed;
        }

        /// <summary>
        /// קודי שגיאה לדוח
        /// </summary>
        public static class Report
        {
            public static readonly ErrorCode Generation_Failed = ErrorCode.Report_Generation_Failed;
        }

        /// <summary>
        /// קודי שגיאה לDB
        /// </summary>
        public static class DB
        {
            public static readonly ErrorCode Connection_Failed = ErrorCode.DB_Connection_Failed;
            public static readonly ErrorCode Query_Failed = ErrorCode.DB_Query_Failed;
            public static readonly ErrorCode Report_NotFound = ErrorCode.DB_Report_NotFound;
            public static readonly ErrorCode Report_Config_Invalid = ErrorCode.DB_Report_Config_Invalid;
            public static readonly ErrorCode ColumnMapping_NotFound = ErrorCode.DB_ColumnMapping_NotFound;
            public static readonly ErrorCode MonthName_NotFound = ErrorCode.DB_MonthName_NotFound;
            public static readonly ErrorCode SugtsName_NotFound = ErrorCode.DB_SugtsName_NotFound;
            public static readonly ErrorCode IshvName_NotFound = ErrorCode.DB_IshvName_NotFound;
            public static readonly ErrorCode MoazaName_NotFound = ErrorCode.DB_MoazaName_NotFound;
            public static readonly ErrorCode StoredProc_MissingParam = ErrorCode.DB_StoredProc_MissingParam;
            public static readonly ErrorCode StoredProc_Execution_Failed = ErrorCode.DB_StoredProc_Execution_Failed;
            public static readonly ErrorCode TableFunc_Execution_Failed = ErrorCode.DB_TableFunc_Execution_Failed;
        }

        /// <summary>
        /// קודי שגיאה לפרמטרים
        /// </summary>
        public static class Parameters
        {
            public static readonly ErrorCode Invalid = ErrorCode.Parameters_Invalid;
            public static readonly ErrorCode Type_Mismatch = ErrorCode.Parameters_Type_Mismatch;
            public static readonly ErrorCode Missing = ErrorCode.Parameters_Missing;
        }
    }
}