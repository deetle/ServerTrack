/////////////////////////////////////////////////////////////////
//
//
//  ServerTrack
//
//
/////////////////////////////////////////////////////////////////



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;



namespace ServerTrack
{


    class Program
    {

       
        //
        // ServerLoad.  
        //  

        public class ServerLoad
        {

        // Member variables  
     
        public DateTime m_Time;
        public double m_CPU_load;
        public double m_RAM_load;

        // Constructor  

        public ServerLoad(
            double CPU_load,
            double RAM_load)
            {
            m_Time = DateTime.Now;  // Time load data was captured  
            m_CPU_load = CPU_load;
            m_RAM_load = RAM_load;
            }

        }


        //
        // Server Load for the current Time Interval
        //

        public class ServerLoadStatsForCurrentTimeInterval
        {

            // Member variables  

            bool m_TimeIsSet;       //  So we only set the first time
            public DateTime m_Time; //  Time of node creation 
            public double m_CPUloadSum;
            public double m_RAMloadSum;
            public int m_ValueCount;
            public double m_CPUloadAverage;
            public double m_RAMloadAverage;


            // Constructor  

            public ServerLoadStatsForCurrentTimeInterval()
            {
                Clear();
            }

            // Methods

            public void Clear()
            {
                m_TimeIsSet = false;
                m_CPUloadSum = 0;
                m_RAMloadSum = 0;
                m_ValueCount = 0;
                m_CPUloadAverage = 0;
                m_RAMloadAverage = 0;
            }

       
            public void Add(ServerLoad Load)
            {

                if (!m_TimeIsSet)
                {
                    m_Time = Load.m_Time;
                    m_TimeIsSet = true;
                }

                m_CPUloadSum = m_CPUloadSum + Load.m_CPU_load;
                m_RAMloadSum = m_RAMloadSum + Load.m_RAM_load;
                m_ValueCount++;

                m_CPUloadAverage = m_CPUloadSum/m_ValueCount;
                m_RAMloadAverage = m_RAMloadSum/m_ValueCount;

            }

            public void UpdateNode(ServerLoadStatsNode Node)
            {
                Node.CPUloadAverage = m_CPUloadAverage;
                Node.RAMloadAverage = m_RAMloadAverage;
            }

            public ServerLoadStatsNode NewNode()
            {
                return new ServerLoadStatsNode(m_CPUloadAverage, m_RAMloadAverage);
            }

        }


        //
        // Server Load Stats node 
        //

        public class ServerLoadStatsNode
        {

            public ServerLoadStatsNode(double _CPU_loadAverage,
                                  double _RAM_loadAverage)
                {
                    CPUloadAverage = _CPU_loadAverage;
                    RAMloadAverage = _RAM_loadAverage;
                }

            public double CPUloadAverage { get; set; }
            public double RAMloadAverage { get; set; }


            }


        //
        // Server Load Stats Que 
        //

        public class ServerLoadStatsQue
        {

            // Member variables 

            Queue<ServerLoadStatsNode> m_Que;
            ServerLoadStatsForCurrentTimeInterval m_Current;
            public TimeSpan m_TimeInterval;
            public int m_Count;
            private Object QueLock = new Object();

            // Constructor 

            public ServerLoadStatsQue(TimeSpan TimeInterval,
                                    int Count)
            {
                m_Que = new Queue<ServerLoadStatsNode>();
                m_Current = new ServerLoadStatsForCurrentTimeInterval();
                m_TimeInterval = TimeInterval;
                m_Count = Count;
            }

            
            // Methods 
 
            public void AddLoad(ServerLoad Load)
            {

            //
            // List empty case 
            //

            lock (QueLock)
            {

                if (m_Que.Count() == 0)
                {
                
                    m_Current.Add(Load);

                    m_Que.Enqueue(m_Current.NewNode());
                    return;
                }

            }

            //
            // New load update from last load update is still with in the 
            // time interval .    Update current node. 
            //

            TimeSpan ElapsedTime = Load.m_Time.Subtract(m_Current.m_Time);

            if (TimeSpan.Compare(ElapsedTime, m_TimeInterval) < 0)
            {

                // update average to current node 

                lock (QueLock)
                {

                    m_Current.Add(Load);

                    m_Current.UpdateNode(m_Que.Peek());

                }

            }
            else
            {

                //
                // New load update from last load update is outside the time  
                // interval . Add new node
                //

                lock (QueLock)
                {

                    m_Current.Clear();

                    m_Current.Add(Load);

                    if (m_Que.Count() >= m_Count)
                    {
                        m_Que.Dequeue();
                    }

                    m_Que.Enqueue(m_Current.NewNode());

                }
            }

            }

        
            public ServerLoadStatsNode[] GetLoadData()
            {

                ServerLoadStatsNode[] LoadData = null;

                lock (QueLock)
                {
                    LoadData = new ServerLoadStatsNode[m_Que.Count()];
                    m_Que.CopyTo(LoadData, 0);
                }
                return LoadData;

            }

        }


        //
        //  Server Node representing a single sever 
        //

        public class ServerNode
        {

            // Member variables 
            
            // Que for 60 minutes broken down into 1 minutes  
            public ServerLoadStatsQue Que60Min;
           
            // Que  24 hours broken down by the hour 
            public ServerLoadStatsQue Que24Hour;

            public String m_Name;

            // Constracture 

            public ServerNode(string Name)
            {
                m_Name = Name;
                Que60Min = new ServerLoadStatsQue(TimeSpan.FromMinutes(1), 60);
                Que24Hour = new ServerLoadStatsQue(TimeSpan.FromHours(1), 24); 
            }

        }

        //
        //  ServerLoadStats returne by Displayloads() 
        //

        public class ServerLoadStats 
            {

            public ServerLoadStatsNode[] Data60Minutes;
            public ServerLoadStatsNode[] Data24Hour;

            };

        //
        //  Server List 
        //

        public class ServerList
        {

            // Member variables 

            List<ServerNode> m_ServerList;
            private Object ServerListLock = new Object();


            // Constracture 

            public ServerList()
            {
                m_ServerList = new List<ServerNode>();
            }

        
            // Methods 

            ServerNode GetServer(String Name)
            {
                //
                // Find or add server 
                //

                lock (ServerListLock)
                {
                    foreach (ServerNode Server in m_ServerList)
                    {
                        if (Server.m_Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return Server;
                        }
                    }

                    ServerNode NewServer  =  new ServerNode(Name);
                    m_ServerList.Add(NewServer);
                    return NewServer;
                }

            }
            public bool Recordload(String Name,ServerLoad Load)
            {


                //
                // Get Server 
                //

                ServerNode Server = GetServer(Name);

                //
                // Update server load data 
                //

                Server.Que60Min.AddLoad(Load);
                Server.Que24Hour.AddLoad(Load);
                 
                return true;
            }

            public bool Displayloads(String Name, ServerLoadStats LoadStats)
            {

                //
                // Get Server 
                //

                ServerNode Server = GetServer(Name);

                //
                // Get server load data 
                //

                LoadStats.Data60Minutes = Server.Que60Min.GetLoadData();
                LoadStats.Data24Hour = Server.Que24Hour.GetLoadData();
                
                return true;
            }

           


        }

        //
        //  Test thread class
        //

        public class TestTheadObject
        {

            // Member variables  

            public ServerList Servers;

            // Methods

            public void RecordloadThread()
            {

                while (true)
                {

                    Servers.Recordload("A", new ServerLoad(2, 3));
                    Servers.Recordload("A", new ServerLoad(4, 6));
                    Servers.Recordload("A", new ServerLoad(8, 12));
                    Servers.Recordload("A", new ServerLoad(16, 24));

                }
            }
        }

        

        static void Main(string[] args)
        {


            ServerList Servers = new ServerList();


            //
            //  Create thread that will call Server.RecoardLoad() 
            //

            TestTheadObject Tto = new TestTheadObject();
            Tto.Servers = Servers;
            Thread oThread = new Thread(new ThreadStart(Tto.RecordloadThread));
            oThread.Start();


            //
            //  Loop calling Server.Displayloads
            //

            while(true)
            {


                //
                //  Get load data 
                //

                ServerLoadStats LoadStats = new ServerLoadStats();

                Servers.Displayloads("A", LoadStats);

                //
                // Display load data 
                //

                Console.WriteLine("\n60 minutes broken down by minute");

                foreach (ServerLoadStatsNode nd in LoadStats.Data60Minutes)
                {
                    Console.WriteLine(" CPU  {0} RAM  {1}", nd.CPUloadAverage, nd.RAMloadAverage);
                }

                Console.WriteLine("24 hours broken down by hour");
                foreach (ServerLoadStatsNode nd in LoadStats.Data24Hour)
                {
                    Console.WriteLine(" CPU  {0} RAM  {1}", nd.CPUloadAverage, nd.RAMloadAverage);
                }
                Thread.Sleep(100);

            }
        }


    }
}
