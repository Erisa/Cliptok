using StackExchange.Redis;
using System.Threading.Tasks;

namespace Cliptok
{
    class ReportDatabase
    {
        public ReportDatabase(IDatabase database)
        {
            db = database;
        }

        /// <summary>
        /// Get or create a report from a generic content ID.
        /// </summary>
        /// <param name="genericID">Content ID.</param>
        /// <param name="key">Report key (the report type actually).</param>
        /// <returns></returns>
        public ulong GetOrSetReportFromContentID(ulong genericID, string key)
        {
            RedisValue reportVal = GetReportFromContentID(genericID, key);
            if (reportVal != RedisValue.Null)
            {
                return (ulong)reportVal;
            }

            return CreateNewReportFromContentID(genericID, key);
        }

        /// <summary>
        /// Recreate a new report from a content ID, deleting the previous one.
        /// </summary>
        /// <param name="genericID">Content ID.</param>
        /// <param name="key">Report key (the report type actually).</param>
        /// <returns></returns>
        public ulong RecreateNewReportFromContentID(ulong genericID, string key)
        {
            DeleteReportHashFromContentID(genericID, key);
            return CreateNewReportFromContentID(genericID, key);
        }

        /// <summary>
        /// Delete report from content ID.
        /// </summary>
        /// <param name="genericID">Content ID.</param>
        /// <param name="key">Report key (the report type actually).</param>
        public void DeleteReportHashFromContentID(ulong genericID, string key)
        {
            RedisValue reportVal = GetReportFromContentID(genericID, key);
            if (reportVal != RedisValue.Null)
            {
                db.HashDelete("reports_pending", reportVal);
                db.HashDelete("reports_reviewed", reportVal);
            }

            db.HashDelete($"rp_{key}", genericID.ToString());
        }

        /// <summary>
        /// Check if a report is pending.
        /// </summary>
        /// <param name="reportID">Report ID to check.</param>
        /// <returns>Whether or not the report is pending.</returns>
        public bool IsPendingReport(ulong reportID)
        {
            RedisValue jsonReport = db.HashGet("reports_pending", reportID);
            return jsonReport != RedisValue.Null;
        }

        /// <summary>
        /// Return a report object from ID (pending).
        /// </summary>
        /// <param name="reportID">ID to check.</param>
        /// <returns>Report object.</returns>
        public string GetPendingReport(ulong reportID)
        {
            RedisValue jsonReport = db.HashGet("reports_pending", reportID);
            // sanity check
            if (jsonReport != RedisValue.Null)
            {
                return jsonReport;
            }

            return null;
        }

        /// <summary>
        /// Return a report object from ID (reviewed).
        /// </summary>
        /// <param name="reportID">ID to check.</param>
        /// <returns>Report object.</returns>
        public string GetReviewedReport(ulong reportID)
        {
            RedisValue jsonReport = db.HashGet("reports_reviewed", reportID);
            if (jsonReport != RedisValue.Null)
            {
                // report has already been reviewed but return it
                return jsonReport;
            }

            return null;
        }

        /// <summary>
        /// Store the report in the pending list.
        /// </summary>
        /// <param name="report">Report to store.</param>
        public async Task SetReportPending(ulong ID, string jsonString)
        {
            await db.HashSetAsync("reports_pending", ID, jsonString);
        }

        /// <summary>
        /// Store the report in the reviewed list.
        /// </summary>
        /// <param name="report">Report to store.</param>
        public async Task SetReportReviewed(ulong ID, string jsonString)
        {
            // report has been reviewed to move it appropriately
            await db.HashSetAsync("reports_reviewed", ID, jsonString);
            _ = db.HashDeleteAsync("reports_pending", ID);
        }

        /// <summary>
        /// Add a report to the user.
        /// </summary>
        /// <param name="report">Report to add.</param>
        /// <param name="status">Where to store the report.</param>
        public async Task AddReportToUser(ulong userID, ReportStatus status)
        {
            string key = GetReportUserKey(status);
            await Program.db.HashIncrementAsync(key, userID);
        }

        /// <summary>
        /// Return the number of reports from this user.
        /// </summary>
        /// <param name="user">The user to get the count from.</param>
        /// <param name="status">Specific report status to count.</param>
        /// <returns></returns>
        public async Task<ulong> GetUserReportCount(ulong userID, ReportStatus status)
        {
            string key = GetReportUserKey(status);
            RedisValue value = await db.HashGetAsync(key, userID);
            if (value != RedisValue.Null)
            {
                return (ulong)value;
            }

            return 0;
        }

        /// <summary>
        /// Return user report key from report status.
        /// </summary>
        /// <param name="status">Report status.</param>
        /// <returns>User key string.</returns>
        private string GetReportUserKey(ReportStatus status)
        {
            return $"reports_users_{status.ToString().ToLower()}";
        }

        /// <summary>
        /// Return a report from a content ID.
        /// </summary>
        /// <param name="genericID">Content ID.</param>
        /// <param name="key">Report key (the report type actually).</param>
        /// <returns></returns>
        private RedisValue GetReportFromContentID(ulong genericID, string key)
        {
            return db.HashGet($"rp_{key}", genericID.ToString());
        }

        /// <summary>
        /// Create a new report from a content ID.
        /// </summary>
        /// <param name="genericID">Content ID.</param>
        /// <param name="key">Report key (the report type actually).</param>
        /// <returns></returns>
        private ulong CreateNewReportFromContentID(ulong genericID, string key)
        {
            ulong reportID = GenerateID();
            db.HashSet($"rp_{key}", genericID.ToString(), reportID);

            return reportID;
        }

        private ulong GenerateID()
        {
            return (ulong)db.StringIncrement("numReports");
        }

        private IDatabase db { get; }
    }
}
