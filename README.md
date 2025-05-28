# Data Platform Live! Orlando 2025

### Hands-On Lab — Building Modern Applications with SQL Server 2025: AI and New Developer Features

Welcome!

Before diving into the hands-on labs, ensure you have the necessary software and sample database installed. Follow the steps below to prepare your environment.

1. **SQL Server 2025**

   A local or cloud-hosted instance of **SQL Server 2025** is required for the labs. You have three options:

    - **Option 1: Local installation on Windows**  

      - Download SQL Server 2025 Developer Edition (free) from: [https://info.microsoft.com/ww-landing-sql-server-2025.html](https://info.microsoft.com/ww-landing-sql-server-2025.html)  

      - Choose the **Basic** option when the installer starts.

    - **Option 2: Local installation with Docker (Linux Container on Windows)**
    
      - Pull the latest SQL Server 2025 container image:
      
        ```powershell
        docker pull mcr.microsoft.com/mssql/server:2025-latest
        ```

    - **Option 3: Azure SQL Database**  
    
      - Alternatively, you may use an Azure SQL Database if you prefer a cloud-hosted environment and you have an Azure subscription. All labs are compatible.

2. **SQL Server Management Studio (SSMS) 21**

   To interact with SQL Server, including running queries and managing databases, install the latest version of SSMS.

    - Download SSMS from:  
[https://aka.ms/ssms/21/release/vs_SSMS.exe](https://aka.ms/ssms/21/release/vs_SSMS.exe)

3. **Visual Studio 2022 (any edition)**

   Visual Studio 2022 is required for the Change Event Streaming labs. 

    - Download the **Community Edition** (free) from:  
  [https://visualstudio.microsoft.com/vs/community/](https://visualstudio.microsoft.com/vs/community/)

    - During installation, select the **.NET Desktop Development** workload.

4. **AdventureWorks2022 Database**
 
   Many labs utilize the AdventureWorks2022 sample database. 

    * **Option 1: SQL Server 2025 on Windows**

      If you're running SQL Server 2025 directly on Windows:

      * **Download the backup file:**
      
        Download the `AventureWorks2022.bak` backup file available [here](https://1drv.ms/f/s!AiiTRkT0Yvc4xd8Kz1oSgzjbselEIA?e=yFaqjc) (right-click and open in a new tab)

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

    * **Option 2: SQL Server 2025 with Docker (Linux Container on Windows)**

      If you're using SQL Server 2025 inside a Linux-based Docker container on Windows:

      * **Download the backup file:**
      
        Download the `AventureWorks2022.bak` backup file available [here](https://1drv.ms/f/s!AiiTRkT0Yvc4xd8Kz1oSgzjbselEIA?e=yFaqjc) (right-click and open in a new tab)

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

    * **Option 3: Azure SQL Database**

      Azure SQL Database does **not** support restoring `.bak` files directly. To load **AdventureWorks2022**, follow these steps using a **DACPAC**:

        1. **Download the AdventureWorks2022 DACPAC**

           * From the official Microsoft sample GitHub:
             [https://github.com/microsoft/sql-server-samples/releases/tag/adventureworks](https://github.com/microsoft/sql-server-samples/releases/tag/adventureworks)

           * Download the file named:
             `AdventureWorks2022.bacpac`
             *(Note: Sometimes DACPAC and BACPAC are both offered. You want the `.bacpac` file for Azure SQL.)*

        2. **Create an Azure SQL Database**

           * In the [Azure Portal](https://portal.azure.com), create a new **Azure SQL Database** (Standard S1 or better is recommended).
           * Choose an existing or new **SQL Server** resource, and create admin credentials.

        3. **Import the BACPAC into Azure SQL Database**

           * In the Azure Portal:

             * Go to your **SQL Server** resource.
             * Under **Settings**, select **Import database**.
             * For **Storage**, select the `.bacpac` file you downloaded earlier (you may need to upload it to a storage container first).
             * Fill in the target database name, pricing tier, and credentials.

           * Alternatively, you can use **SQL Server Management Studio (SSMS)**:

             * Right-click on **Databases** > **Import Data-tier Application...**
             * Follow the wizard to import the `.bacpac` into your Azure SQL Database.

        4. **Connect to your Azure SQL Database in SSMS**

           * Use the **fully qualified server name** (e.g., `yourserver.database.windows.net`).
           * Use the SQL admin login and password you created.
           * You should now see the restored **AdventureWorks2022** database ready for use in the lab.

---

## You're all set.

Ready to dive in?

▶ [Let's get started!](https://github.com/lennilobel/sql2025-workshop-hol-orlando2025/blob/main/HOL)
