using Antlr4.StringTemplate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zio;

namespace de.creinbold.FlatShare
{
    public class TaxReportTexWriter
    {
        private IFileSystem _TexDump;

        private struct ProviderLine
        {
            public string Name;
            public IEnumerable<string> Services;
            public string Cost;
        }

        private struct ProviderTransactionLine
        {
            public string Date;
            public string Provider;
            public string Cost;
        }

        private struct DepostLine
        {
            public string Date;
            public string Tenant;
            public string Obtained;
            public string Refunded;
            public string Retained;
            public string Ancillary;
        }

        private struct TenantTransaction
        {
            public string Date;
            public string Tenant;
            public string Amount;
            public IEnumerable<TenantTransactionLine> Lines;
        }

        private struct TenantTransactionLine
        {
            public string Amount;
            public string Reason;
            public string Due;
            public string Net;
            public string Ancillary;
            public string OtherYear;
            public bool IsOverwriting;
            public bool IsImportant;
        }

        private static IFormat F = Format.CURRENT;

        public TaxReportTexWriter(IFileSystem texDump = null)
        {
            _TexDump = texDump;
        }

        public string Extension { get { return "pdf"; } }

        public bool Write(TaxReport report, Stream outStream, bool verbose)
        {
            var texSource = BuildTexSource(report);
            UPath texSourcePath = "/report.tex";

            using (var compiler = new TexCompiler())
            {
                compiler.SourcePath = texSourcePath;
                using (var writer = new StreamWriter(compiler.WorkingDirectory.OpenFile(texSourcePath, FileMode.CreateNew, FileAccess.Write)))
                {
                    writer.Write(texSource);
                }
                var success = compiler.TryToCompile(2, verbose);
                if (_TexDump != null)
                {
                    try
                    {
                        UPath targetDir = "/" + report.FileName;
                        compiler.CopyWorkingDirectory(_TexDump, targetDir);
                    }
                    catch { }
                }

                if (success)
                {
                    try
                    {
                        using (var compiledStream = compiler.WorkingDirectory.OpenFile(compiler.CompiledPath, FileMode.Open, FileAccess.Read))
                        {
                            compiledStream.CopyTo(outStream);
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }
                return success;
            }
        }

        private string BuildTexSource(TaxReport report)
        {
            var templateString = TexCompiler.PreprocessTemplateString(Properties.Resources.TexTax);
            var helperTemplateString = TexCompiler.PreprocessTemplateString(Properties.Resources.TexTaxHelpers);

            var templateGroup = new TemplateGroupString(helperTemplateString);
            var template = new Template(templateGroup, templateString);

            template.Add("year", report.Year);
            template.Add("currency_symbol", Currency.CURRENT.TexSymbol);
            AddProviderData(template, report);
            AddTenantData(template, report);
            AddDepositData(template, report);

            return template.Render();
        }

        private void AddProviderData(Template template, TaxReport report)
        {
            // Prevent "attribute not defined" string template error
            template.Add("provider_lines", null);
            template.Add("provider_transaction_lines", null);

            foreach (var entry in report.ProviderEntries)
            {
                var line = new ProviderLine();
                line.Name = TexCompiler.EscapeSpecialCharacters(entry.Provider);
                line.Services = entry.Services.Select(s => TexCompiler.EscapeSpecialCharacters(s));
                line.Cost = F.CostToString(entry.Amount);
                template.Add("provider_lines", line);
            }
            foreach (var entry in report.ProviderTransactions)
            {
                var line = new ProviderTransactionLine();
                line.Date = F.DateToString(entry.Date);
                line.Provider = TexCompiler.EscapeSpecialCharacters(entry.Provider);
                line.Cost = F.CostToString(entry.Amount);
                template.Add("provider_transaction_lines", line);
            }
            var amount_sum = report.ProviderEntries.Select(e => e.Amount).Sum();
            template.Add("provider_transaction_sum", F.CostToString(amount_sum));
        }

        private void AddTenantData(Template template, TaxReport report)
        {
            // Prevent "attribute not defined" string template error
            template.Add("tenant_transactions", null);

            decimal netSum = 0M;
            decimal ancillarySum = 0M;
            foreach (var transaction in report.TenantTransactions)
            {
                var transactionStrings = new TenantTransaction();
                transactionStrings.Date = F.DateToString(transaction.Date);
                transactionStrings.Tenant = TexCompiler.EscapeSpecialCharacters(transaction.Tenant.Name);
                transactionStrings.Amount = F.CostToString(transaction.Amount);
                var lines = new List<TenantTransactionLine>();
                foreach (var assignment in transaction.Assignments)
                {
                    var line = new TenantTransactionLine();
                    line.Amount = F.CostToString(assignment.AssignedAmount);
                    line.Reason = TexCompiler.EscapeSpecialCharacters(
                        F.ReasonToString(assignment.Reason, out line.IsOverwriting, out line.IsImportant));
                    if (assignment.Due.HasValue) line.Due = F.DateToString(assignment.Due.Value);
                    if (assignment.TaxYear != report.Year) line.OtherYear = assignment.TaxYear.ToString();
                    if (!line.IsOverwriting)
                    {
                        if (assignment.TaxYear == report.Year)
                        {
                            netSum += assignment.NetTax.GetValueOrDefault();
                            ancillarySum += assignment.AncillaryTax.GetValueOrDefault();
                        }
                        line.Net = assignment.NetTax.HasValue ? F.CostToString(assignment.NetTax) : "";
                        line.Ancillary = assignment.AncillaryTax.HasValue ? F.CostToString(assignment.AncillaryTax) : "";
                    }
                    lines.Add(line);
                }
                if (transaction.Unassigned != 0)
                {
                    Console.WriteLine(String.Format("Warning: (Partially) unassigned transaction at {0}.", DateUtils.DateAsString(transaction.Date)));
                    var line = new TenantTransactionLine();
                    line.Amount = F.CostToString(transaction.Unassigned);
                    line.Reason = TexCompiler.EscapeSpecialCharacters(
                        F.ReasonToString(null, out line.IsOverwriting, out line.IsImportant));
                    lines.Add(line);
                }
                transactionStrings.Lines = lines;
                template.Add("tenant_transactions", transactionStrings);
            }
            template.Add("net_sum", F.CostToString(netSum));
            template.Add("ancillary_sum", F.CostToString(ancillarySum));
        }

        private void AddDepositData(Template template, TaxReport report)
        {
            decimal obtainedSum = 0M;
            decimal refundedSum = 0M;
            decimal retainedSum = 0M;
            decimal ancillarySum = 0M;

            // Prevent "attribute not defined" string template error
            template.Add("deposit_lines", null);

            foreach (var entry in report.DepositEntries)
            {
                var line = new DepostLine();
                line.Date = F.DateToString(entry.Due);
                line.Tenant = TexCompiler.EscapeSpecialCharacters(entry.Tenant.Name);
                line.Obtained = F.CostToString(entry.ObtainedAmount);
                line.Refunded = F.CostToString(entry.RefundedAmount);
                line.Retained = F.CostToString(entry.RetainedAmount);
                line.Ancillary = F.CostToString(entry.AncillaryAmount);
                obtainedSum += entry.ObtainedAmount;
                refundedSum += entry.RefundedAmount;
                retainedSum += entry.RetainedAmount;
                ancillarySum += entry.AncillaryAmount;
                template.Add("deposit_lines", line);
            }
            template.Add("deposit_obtained_sum", F.CostToString(obtainedSum));
            template.Add("deposit_refunded_sum", F.CostToString(refundedSum));
            template.Add("deposit_retained_sum", F.CostToString(retainedSum));
            template.Add("deposit_ancillary_sum", F.CostToString(ancillarySum));
        }
    }
}
