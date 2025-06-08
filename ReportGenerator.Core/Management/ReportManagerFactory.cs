using ReportGenerator.Core.Configuration;
using ReportGenerator.Core.Generators;
using System;
using System.IO;

namespace ReportGenerator.Core.Management
{
    public static class ReportManagerFactory
    {
        public static ReportManager CreateReportManager(
            string connectionString, 
            string templatesFolder, 
            string outputFolder,
            IHtmlToPdfConverter htmlToPdfConverter = null)
        {
            return new ReportManager(
                connectionString, 
                templatesFolder, 
                outputFolder, 
                htmlToPdfConverter);
        }

        public static ReportManager CreateReportManager(string connectionString, string templatesFolder, string outputFolder)
        {
            return new ReportManager(connectionString, templatesFolder, outputFolder);
        }
    }
}
