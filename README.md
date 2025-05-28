# Data Platform Live! Orlando 2025

### Hands-On Lab — Building Modern Applications with SQL Server 2025: AI and New Developer Features

Welcome!

Before diving into the hands-on labs, ensure you have the necessary software and sample database installed. Follow the steps below to prepare your environment.

1. **SQL Server 2025**: A local or cloud-hosted instance of **SQL Server 2025** is required for the labs. You have two options:

    - **Local installation (Windows or Docker for Windows):**  
    Download SQL Server 2025 Developer Edition (free) from:<br>
      [https://info.microsoft.com/ww-landing-sql-server-2025.html](https://info.microsoft.com/ww-landing-sql-server-2025.html)  
  Choose the **Basic** option when the installer starts.

    - **Azure SQL Database:**  
  Alternatively, you may use an Azure SQL Database if you prefer a cloud-hosted environment. All labs are compatible.

2. **SQL Server Management Studio (SSMS) 21**: To interact with SQL Server, including running queries and managing databases, install the latest version of SSMS.

    - Download SSMS from:  
[https://aka.ms/ssms/21/release/vs_SSMS.exe](https://aka.ms/ssms/21/release/vs_SSMS.exe)

3. **Visual Studio 2022 (any edition)** is required for the Change Event Streaming labs. 

    - Download the **Community Edition** (free) from:  
  [https://visualstudio.microsoft.com/vs/community/](https://visualstudio.microsoft.com/vs/community/)

    - During installation, select the **.NET Desktop Development** workload.

4. **AdventureWorks2022 Database**: Many labs utilize the AdventureWorks2022 sample database. Download the `AventureWorks2022.bak` backup file available [here](https://1drv.ms/f/s!AiiTRkT0Yvc4xd8Kz1oSgzjbselEIA?e=yFaqjc) (right-click and open in a new tab)

    * **Option A: SQL Server 2025 on Windows**

      * **Create a folder:**  
   Create a folder on your C: drive called `C:\HolDB`.

      * **Copy the backup file:**  
           Move `AdventureWorks2022.bak` from your Downloads folder into `C:\HolDB`.

      * **Restore in SSMS:**
           - Launch SSMS and connect to your local SQL Server instance.
           - Right-click on **Databases** and choose **Restore Database...**
           - Select **Device**, click `...`, then click **Add**.
           - Navigate to `C:\HolDB`, select the `.bak` file, and click **OK**.
           - Click **OK** again to start the restore.

    * **Option B: SQL Server 2025 with Docker (Linux Container on Windows)**

      If you're using SQL Server 2025 inside a Linux-based Docker container on Windows:

      * **Create a shared folder:**  
   Create a host folder (e.g., `C:\Temp`) and copy `AdventureWorks2022.bak` into it.

      * **Start the SQL Server container:**
        ```powershell
        docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=<your-sa-password>" -p 1433:1433 -v C:\Temp:/var/opt/mssql/backup --name sql2025 -d mcr.microsoft.com/mssql/server:2025-latest
        ```

      * **Restore in SSMS:**

        - Launch SSMS.
        - Connect to `localhost,1433`.
        - Right-click on **Databases** and choose **Restore Database...**
        - Select **Device**, click `...`, then click **Add**.
        - Navigate to `/var/opt/mssql/backup/AdventureWorks2022.bak`
        - Click **OK** twice to return to the main restore dialog.
        - Click **OK** to begin the restore.

      * **Alternatively, restore using T-SQL:**

           ```sql
           RESTORE DATABASE AdventureWorks2022
           FROM DISK = N'/var/opt/mssql/backup/AdventureWorks2022.bak'
           WITH MOVE 'AdventureWorks2022' TO '/var/opt/mssql/data/AdventureWorks2022.mdf',
                MOVE 'AdventureWorks2022_log' TO '/var/opt/mssql/data/AdventureWorks2022_log.ldf';
           ```

---

## You're all set.

Ready to dive in?

▶ [Let's get started!](https://github.com/lennilobel/sql2025-workshop-hol-orlando2025/blob/main/HOL)
