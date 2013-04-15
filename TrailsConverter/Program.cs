using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using CesiumLanguageWriter;
using System.Drawing;
using System.Device.Location;

namespace TrailsConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            System.IO.TextWriter textWriter = System.IO.File.CreateText(@"G:\Utah_2013\TrailsConverter\DeerValleyTrailsAltitude.czml");
            CesiumOutputStream output = new CesiumOutputStream(textWriter);
            output.PrettyFormatting = true;
            output.WriteStartSequence();

            CesiumStreamWriter cesiumWriter = new CesiumStreamWriter();

            using (StreamReader reader = File.OpenText(@"G:\Utah_2013\TrailsConverter\DeerValleyTrails.czml"))
            {
                JArray o = (JArray)JToken.ReadFrom(new JsonTextReader(reader));
                int numTracks = o.Children().Count();
                foreach (JToken token in o.Children())
                {
                    System.Console.WriteLine(numTracks);
                    --numTracks;
                    string trailName = (string)token["label"]["text"];
                    int red = (int)token["polyline"]["color"]["rgba"][0];
                    int green = (int)token["polyline"]["color"]["rgba"][1];
                    int blue = (int)token["polyline"]["color"]["rgba"][2];
                    float width = (float)token["polyline"]["width"];

                    List<GeoCoordinate> cartList = new List<GeoCoordinate>();
                    JArray positions = (JArray)token["vertexPositions"]["cartographicRadians"];
                    int count = 0;
                    GeoCoordinate lla = new GeoCoordinate();
                    foreach (JToken value in positions.Children())
                    {
                        switch (count)
                        {
                            case 0:
                                lla.Longitude = ((double)value) * 180.0 / Math.PI;
                                ++count;
                                break;
                            case 1:
                                lla.Latitude = ((double)value) * 180.0 / Math.PI;
                                ++count;
                                break;
                            case 2:
                                //lla.Altitude = (double)value;
                                cartList.Add(lla);
                                lla = new GeoCoordinate();
                                count = 0;
                                break;
                        }
                    }

                    // add extra points for higher resolution
                    List<GeoCoordinate> expandedList = new List<GeoCoordinate>();
                    for(int i=0; i<cartList.Count(); ++i) 
                    {
                        if (i > 0)
                        {
                            GeoCoordinate prevPoint = cartList[i - 1];
                            GeoCoordinate curPoint = cartList[i];
                            double distance = prevPoint.GetDistanceTo(curPoint);
                            if (distance > 25.0)
                            {
                                double segments = Math.Ceiling(distance / 25.0);
                                double deltaLat = (curPoint.Latitude - prevPoint.Latitude) / segments;
                                double deltaLon = (curPoint.Longitude - prevPoint.Longitude) / segments;

                                for (int segNum = 1; segNum < segments; ++segNum)
                                {
                                    GeoCoordinate newCoord = new GeoCoordinate(
                                        prevPoint.Latitude + (deltaLat * segNum),
                                        prevPoint.Longitude + (deltaLon * segNum));
                                    expandedList.Add(newCoord);
                                }
                            }

                        }
                        expandedList.Add(cartList[i]);
                    }

                    List<Cartographic> vertexList = new List<Cartographic>();
                    foreach (GeoCoordinate cart in expandedList)
                    {
                        cart.Latitude = cart.Latitude;
                        cart.Longitude = cart.Longitude;

                        string url = "http://gisdata.usgs.net/xmlwebservices2/elevation_service.asmx/getElevation?" +
                            "X_Value=" + cart.Longitude.ToString() +
                            "&Y_Value=" + cart.Latitude.ToString() +
                            "&Elevation_Units=meters&Source_Layer=&Elevation_Only=true";



                        WebRequest wrGETURL = WebRequest.Create(url);
                        Stream response = wrGETURL.GetResponse().GetResponseStream();
                        StreamReader responseReader = new StreamReader(response);
                        responseReader.ReadLine(); // ignore first line
                        string line = responseReader.ReadLine();
                        line = line.Remove(0, 8);
                        line = line.Remove(line.IndexOf("<"));
                        cart.Altitude = Convert.ToDouble(line) - 15;

                        Cartographic temp = new Cartographic(cart.Longitude, cart.Latitude, cart.Altitude);
                        vertexList.Add(temp);
                    }


                    // write czml
                    PacketCesiumWriter packetWriter = cesiumWriter.OpenPacket(output);
                    packetWriter.WriteId(trailName);

                    PolylineCesiumWriter polyline = packetWriter.OpenPolylineProperty();
                    polyline.WriteWidthProperty(width);
                    polyline.WriteColorProperty(Color.FromArgb(red, green, blue));
                    polyline.Close();

                    PositionListCesiumWriter vertices = packetWriter.OpenVertexPositionsProperty();
                    vertices.WriteCartographicDegrees(vertexList);
                    vertices.Close();


                    packetWriter.Close();

                }
            }

            output.WriteEndSequence();
            textWriter.Close();
        }
    }
}
