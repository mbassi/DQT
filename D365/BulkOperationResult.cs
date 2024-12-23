using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQT.D365
{
    /// <summary>
    /// Represents the result of a bulk operation execution
    /// </summary>
    public class BulkOperationResult
    {
        /// <summary>
        /// Gets or sets whether the overall operation was successful (no failures)
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the number of successfully processed operations
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of failed operations
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the collection of failed operations with their corresponding errors
        /// </summary>
        public IReadOnlyCollection<(OrganizationRequest Request, Exception Error)> Failures { get; set; }

        /// <summary>
        /// Creates a new instance of BulkOperationResult
        /// </summary>
        public BulkOperationResult()
        {
            Failures = Array.Empty<(OrganizationRequest, Exception)>();
        }
    }

}
