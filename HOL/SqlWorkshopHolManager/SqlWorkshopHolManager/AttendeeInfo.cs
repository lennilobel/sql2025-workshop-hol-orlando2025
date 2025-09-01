namespace SqlHolWorkshopLabManager
{
	public class AttendeeInfo
	{
		public string AttendeeName { get; init; }
		public string SqlDatabaseServerName { get; set; }
		public string EventHubNamespaceName { get; set; }
		public string EventHubSasToken { get; set; }
		public string StorageAccountConnectionString { get; set; }

		public AttendeeInfo(string attendeeName)
		{
			this.AttendeeName = attendeeName;
		}
	}

}
