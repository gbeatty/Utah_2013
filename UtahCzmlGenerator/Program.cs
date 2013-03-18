using CesiumLanguageWriter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UtahCzmlGenerator.videoTimesDataSetTableAdapters;

namespace UtahCzmlGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            System.IO.TextWriter textWriter = System.IO.File.CreateText("alta.czml");
            CesiumOutputStream output = new CesiumOutputStream(textWriter);
            output.PrettyFormatting = true;
            output.WriteStartSequence();

            CesiumStreamWriter writer = new CesiumStreamWriter();
            
            VideoDataTableAdapter table = new VideoDataTableAdapter();
            videoTimesDataSet.trackTimesDataTable trackTimes = new trackTimesTableAdapter().GetData();
            
            
            foreach (var v in table.GetData())
            {
                if (v.FileName.Contains("Alta"))
                {
                    var fileName = v.FileName;
                    PacketCesiumWriter packetWriter = writer.OpenPacket(output);
                    packetWriter.WriteId(fileName);

                    JulianDate startTime = new JulianDate(v.DateUTC);
                    TimeSpan timeSpan = new TimeSpan(v.Duration.Hour, v.Duration.Minute, v.Duration.Second);
                    Duration duration = new Duration(timeSpan);
                    packetWriter.WriteAvailability(startTime, startTime.Add(duration));

                    ScreenOverlayCesiumWriter overlay = packetWriter.ScreenOverlayWriter;
                    overlay.Open(output);

                    overlay.WriteWidthProperty(500);
                    overlay.WriteHeightProperty(340);
                    overlay.WriteShowProperty(true);
                    overlay.WritePositionProperty(new Rectangular(20, 120));

                    MaterialCesiumWriter material = overlay.OpenMaterialProperty();
                    VideoMaterialCesiumWriter video = material.OpenVideoProperty();
                    video.WriteLoopProperty(true);
                    video.WriteSpeedProperty(1.0);
                    video.WriteStartTimeProperty(startTime);
                    video.WriteVideoProperty("videos/" + fileName, CesiumResourceBehavior.LinkTo);
                    video.Close();
                    material.Close();

                    overlay.Close();
                    packetWriter.Close();


                    var query =
                        (from r in trackTimes.AsEnumerable()
                         where r.time >= v.DateUTC
                         orderby r.time ascending
                         select new { r.time, r.latitude, r.longitude, r.altitude }).First();

                    JulianDate billboardDate = new JulianDate(query.time);
                    packetWriter = writer.OpenPacket(output);
                    packetWriter.WriteId(billboardDate.ToGregorianDate().ToIso8601String());
                    packetWriter.WritePositionPropertyCartographicRadians(
                        new Cartographic(query.longitude, query.latitude, query.altitude));
                    
                    BillboardCesiumWriter billboard = packetWriter.BillboardWriter;
                    billboard.Open(output);
                    billboard.WriteImageProperty("film.png", CesiumResourceBehavior.LinkTo);
                    billboard.WriteScaleProperty(0.25);
                    billboard.Close();
                    packetWriter.Close();

                }
            }

            output.WriteEndSequence();
            textWriter.Close();

        }
    }
}
