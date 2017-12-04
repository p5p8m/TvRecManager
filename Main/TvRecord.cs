//***********************************************************************************************************
// Revision       $Revision: 26014 $
// Last Modified  $Date: 2015-06-01 16:55:35 +0200 (Mo, 01. Jun 2015) $
// Author         $Author: pascal.melix $
// File           $URL: https://csvnhou-pro.houston.hp.com:18490/svn/sa_paf-tsrd/storage/source/trunk/sanxpert/Code/gui/sanreporter/AttributeReportGenerator.cs $
//***********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TvRecManager
{
    public class TvRecord
    {
        public int Duration { get; private set; }
        public string Channel { get; private set; }
        public string Titel { get; private set; }
        public string Day { get; private set; }
        public string StartTime { get; private set; }
        public string EndTime { get; private set; }
        public string Location { get; private set; }
        public string Position { get; set; }
        public TvRecord(int duration, string channel, string titel, string day, string startTime, string endTime, string location)
        {
            Duration = duration;
            Channel = channel;
            Titel = titel;
            Day = day;
            StartTime = startTime;
            EndTime = endTime;
            Location = location;
            Position = null;
        }
        public TvRecord(TvRecord src)
        {
            if (src != null)
            {
                Duration = src.Duration;
                Channel = src.Channel;
                Titel = src.Titel;
                Day = src.Day;
                StartTime = src.StartTime;
                EndTime = src.EndTime;
                Location = src.Location;
            }
        }
        public string VisText
        {
            get
            {
                return (String.Format(@"{0} ({1})", Titel, Day));
            }
        }
    }
    public class TvRecordsList : List<TvRecord>
    {
        public TvRecordsList()
            : base()
        {
        }
        public TvRecordsList(TvRecordsList src)
        {
            if (src != null)
            {
                foreach (TvRecord rec in src)
                {
                    this.Add(rec == null ? null : new TvRecord(rec));
                }
            }
        }
    }
}
