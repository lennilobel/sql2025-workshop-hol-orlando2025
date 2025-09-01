namespace SqlHolWorkshopLabManager
{
	public class AttendeeInfo
	{
		public string AttendeeName { get; }
		public string EmailAddress { get; }
		public string SqlDatabaseServerName { get; set; }
		public string EventHubNamespaceName { get; set; }
		public string EventHubSasToken { get; set; }
		public string StorageAccountConnectionString { get; set; }

		public AttendeeInfo(string attendeeName, string emailAddress = null)
		{
			this.AttendeeName = attendeeName;
			this.EmailAddress = emailAddress;
		}
	}

}
