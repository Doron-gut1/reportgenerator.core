using System;
using System.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Font;
using iText.IO.Font;
using System.Reflection;

namespace ReportGenerator.Core.Generators
{
    public class PdfGenerator
    {
        private readonly string _templatePath;
        private PdfFont _font;

        public PdfGenerator(string templatePath)
        {
            _templatePath = templatePath ?? throw new ArgumentNullException(nameof(templatePath));
            string fontPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Arial.ttf");
            _font = PdfFontFactory.CreateFont(
                        fontPath,                                           // נתיב הקובץ
                        PdfEncodings.IDENTITY_H,                           // תמיכה בעברית
                        PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED    // אסטרטגיית הטמעה
                    );
        }

        public byte[] Generate(DataTable data)
        {
            if (data == null || data.Rows.Count == 0)
                throw new ArgumentException("Data table is empty");

            using var memoryStream = new MemoryStream();
            try
            {
                using var pdfReader = new PdfReader(_templatePath);
                using var pdfWriter = new PdfWriter(memoryStream);
                using var pdfDoc = new PdfDocument(pdfReader, pdfWriter);

                var form = PdfAcroForm.GetAcroForm(pdfDoc, true);
                if (form == null)
                    throw new Exception("No form fields found in template");

                var fields = form.GetFormFields();

                // מילוי השדות
                foreach (var field in fields)
                {
                    string fieldName = field.Key;
                    var formField = field.Value;

                    if (data.Columns.Contains(fieldName))
                    {
                        try
                        {
                            var value = data.Rows[0][fieldName]?.ToString() ?? string.Empty;

                            try
                            {
                                formField.SetFont(_font);
                            }
                            catch
                            {
                                // ממשיך גם אם הגדרת הפונט נכשלה
                            }

                            // טיפול בטקסט עברי והכנסה לשדה
                            formField.SetValue(ReverseOnlyHebrew(value));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to set field {fieldName}: {ex.Message}");
                            continue;
                        }
                    }
                }

                form.FlattenFields();
                pdfDoc.Close();

                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating PDF: {ex.Message}", ex);
            }
        }

        private string ReverseOnlyHebrew(string str)
        {
            // אם הטקסט ריק או מספר בלבד - להחזיר כמו שהוא
            if (string.IsNullOrEmpty(str)) return str;
            if (decimal.TryParse(str, out decimal _)) return str;

            // פיצול לפי תבנית: רווחים, אותיות עבריות, אחוז, ושמירת המפרידים
            var parts = Regex.Split(str, @"( )|([א-ת]+)|(%)");

            var result = new StringBuilder();

            // מעבר מהסוף להתחלה
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                var part = parts[i];

                if (string.IsNullOrEmpty(part)) continue;

                // רווח - להוסיף כמו שהוא
                if (part == " ")
                {
                    result.Append(" ");
                    continue;
                }

                part = part.Trim();
                if (string.IsNullOrEmpty(part)) continue;

                // בדיקה אם מספר שלם או עשרוני
                if (int.TryParse(part, out int intValue))
                {
                    result.Append(intValue);
                    continue;
                }
                if (decimal.TryParse(part, out decimal decValue))
                {
                    result.Append(decValue);
                    continue;
                }

                // בדיקה אם טקסט באנגלית
                byte[] firstChar = Encoding.ASCII.GetBytes(part.Substring(0, 1));
                bool isEnglish = (firstChar[0] >= 48 && firstChar[0] <= 57) ||  // מספרים
                                (firstChar[0] >= 65 && firstChar[0] <= 90) ||  // אותיות גדולות
                                (firstChar[0] >= 97 && firstChar[0] <= 122);   // אותיות קטנות

                // אם אנגלית - להשאיר כמו שזה, אחרת - להפוך
                result.Append(isEnglish ? part : ReverseString(part));
            }

            return result.ToString();
        }

        // פונקציית עזר להיפוך מחרוזת
        private string ReverseString(string str)
        {
            char[] chars = str.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }
}