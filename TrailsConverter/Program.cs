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
using SharpKml.Engine;
using System.Xml;

namespace TrailsConverter
{
    class Program
    {
        static KmlFile ParseLegacyFile(String kmlString)
        {
            // Manually parse the Xml
            SharpKml.Base.Parser parser = new SharpKml.Base.Parser();
            parser.ParseString(kmlString, false); // Ignore the namespaces

            // The element will be stored in parser.Root - wrap it inside
            // a KmlFile
            return KmlFile.Create(parser.Root, true);
        }

        static void Main(string[] args)
        {
            System.IO.TextWriter textWriter = System.IO.File.CreateText(@"D:\Cesium\gbeatty-cesium\Apps\SkiTracks\Gallery\DeerValleyTrails.czml");
            CesiumOutputStream output = new CesiumOutputStream(textWriter);
            output.PrettyFormatting = true;
            output.WriteStartSequence();

            CesiumStreamWriter cesiumWriter = new CesiumStreamWriter();

            using (StreamReader reader = File.OpenText("D:/Cesium/Utah_2013/TrailsConverter/DeerValleyTrails.kml"))
            {
                KmlFile kmlFile = ParseLegacyFile(reader.ReadToEnd());

                var inlined = StyleResolver.InlineStyles(kmlFile.Root);
                SharpKml.Dom.Kml kml = inlined as SharpKml.Dom.Kml;

                int numLines = 0;
                foreach (var lineString in kml.Flatten().OfType<SharpKml.Dom.LineString>())
                {
                    ++numLines;
                }

                int lineNum = 0;
                foreach (var lineString in kml.Flatten().OfType<SharpKml.Dom.LineString>())
                {
                    double percentComplete = (double)lineNum / (double)numLines * 100.0;
                    ++lineNum;
                    System.Console.Write(percentComplete);
                    System.Console.WriteLine("%");

                    List<GeoCoordinate> cartList = new List<GeoCoordinate>();
                    foreach (var point in lineString.Coordinates)
                    {
                        GeoCoordinate lla = new GeoCoordinate();
                        lla.Latitude = point.Latitude;
                        lla.Longitude = point.Longitude;
                        lla.Altitude = 0.0;
                        cartList.Add(lla);
                    }

                    // add extra points for higher resolution
                    List<GeoCoordinate> expandedList = new List<GeoCoordinate>();
                    for (int i = 0; i < cartList.Count(); ++i)
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

                        string url = "http://ned.usgs.gov/epqs/pqs.php?" +
                            "x=" + cart.Longitude.ToString() +
                            "&y=" + cart.Latitude.ToString() +
                            "&units=Meters&output=xml";



                        WebRequest wrGETURL = WebRequest.Create(url);
                        Stream response = wrGETURL.GetResponse().GetResponseStream();
                        StreamReader responseReader = new StreamReader(response);

                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(responseReader.ReadToEnd());
                        XmlNode first = doc.SelectSingleNode("USGS_Elevation_Point_Query_Service");
                        XmlNode elevation = first.FirstChild.SelectSingleNode("Elevation");

                        cart.Altitude = Convert.ToDouble(elevation.InnerText) - 10.0;

                        Cartographic temp = new Cartographic(cart.Longitude, cart.Latitude, cart.Altitude);
                        vertexList.Add(temp);
                    }

                    SharpKml.Dom.Feature feature = lineString.GetParent<SharpKml.Dom.Feature>();
                    SharpKml.Dom.Style style = StyleResolver.CreateResolvedStyle(
                        feature,
                        kmlFile,
                        SharpKml.Dom.StyleState.Normal, // or StyleState.Highlight
                        null); // Don't look for external references


                    String trailName = feature.Name;
                    double width = (double)style.Line.Width;
                    int red = style.Line.Color.Value.Red;
                    int green = style.Line.Color.Value.Green;
                    int blue = style.Line.Color.Value.Blue;
                    int alpha = style.Line.Color.Value.Alpha;

                    // write czml
                    PacketCesiumWriter packetWriter = cesiumWriter.OpenPacket(output);
                    packetWriter.WriteId("document");
                    packetWriter.WriteName("DeerValleyTrails");
                    packetWriter.WriteVersion("1.0");
                    packetWriter.Close();

                    packetWriter = cesiumWriter.OpenPacket(output);
                    packetWriter.WriteId(trailName);

                    PolylineCesiumWriter polyline = packetWriter.OpenPolylineProperty();
                    polyline.WriteWidthProperty(width);

                    PolylineMaterialCesiumWriter material = polyline.OpenMaterialProperty();
                    PolylineOutlineMaterialCesiumWriter outline = material.OpenPolylineOutlineProperty();
                    outline.WriteOutlineWidthProperty(3.0);
                    outline.WriteOutlineColorProperty(255, 255, 255, 140);
                    outline.WriteColorProperty(red, green, blue, alpha);
                    outline.Close();
                    material.Close();

                    polyline.WritePositionsPropertyCartographicDegrees(vertexList);

                    polyline.Close();

                    packetWriter.Close();

                }

            }

            output.WriteEndSequence();
            textWriter.Close();
        }
    }
}
