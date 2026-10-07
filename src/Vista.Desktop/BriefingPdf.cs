using System.Globalization;
using System.Text;
using System.IO;
namespace Vista.Desktop;
public sealed record BriefingPdfSection(string Title,string Text);
public static class BriefingPdf
{
 public static void Write(string path,string title,string state,IReadOnlyList<BriefingPdfSection> sections)
 {
  var pages=new List<string>();var current=new StringBuilder();double y=0;int page=0;
  string N(double n)=>n.ToString("0.##",CultureInfo.InvariantCulture);
  string Clean(string text)=>new(text.Replace("→"," to ").Replace("—","-").Replace("–","-").Replace("·"," / ").Replace("’","'").Replace("✓","[OK]").Replace("•","-").Select(c=>c is >= ' ' and <= '~'||c is >= '\u00a0' and <= '\u00ff'?c:' ').ToArray());
  void Text(string t,double x,double at,double size,bool bold=false)
  {var safe=Clean(t).Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)");current.Append($"BT /{(bold?"F2":"F1")} {N(size)} Tf {N(x)} {N(at)} Td ({safe}) Tj ET\n");}
  void NewPage()
  {
   if(page>0){Text($"VISTA / Simulator exercise briefing / Page {page}",44,25,8);pages.Add(current.ToString());current.Clear();}
   page++;current.Append("0.05 0.09 0.15 rg 0 760 595 82 re f\n0.53 0.81 0.92 rg\n");Text("VISTA",44,805,24,true);Text("SORTIE BRIEFING",44,783,9);Text(DateTimeOffset.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'"),360,786,9);current.Append("0.08 0.13 0.20 rg\n");y=730;
  }
  IEnumerable<string> Lines(string text,double size,bool bold)
  {
   double Width(string value)=>new System.Windows.Media.FormattedText(value,CultureInfo.InvariantCulture,System.Windows.FlowDirection.LeftToRight,new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"),System.Windows.FontStyles.Normal,bold?System.Windows.FontWeights.Bold:System.Windows.FontWeights.Normal,System.Windows.FontStretches.Normal),size,System.Windows.Media.Brushes.Black,1).WidthIncludingTrailingWhitespace;
   foreach(var paragraph in text.Replace("\r","").Split('\n'))
   {
    var rest=Clean(paragraph);if(rest.Length==0){yield return "";continue;}
    while(Width(rest)>507)
    {
     int lo=1,hi=rest.Length;while(lo<hi){int mid=(lo+hi+1)/2;if(Width(rest[..mid])<=507)lo=mid;else hi=mid-1;}
     var cut=rest.LastIndexOf(' ',lo-1,lo);if(cut<lo/3)cut=lo;
     yield return rest[..cut];rest=rest[cut..].TrimStart();
    }
    yield return rest;
   }
  }
  void Paragraph(string text,double size=10,bool bold=false)
  {foreach(var line in Lines(text,size,bold)){if(y<65)NewPage();Text(line,44,y,size,bold);y-=size+5;}}
  NewPage();Paragraph(title,18,true);y-=4;Paragraph(state,10,true);Paragraph("Exported snapshot - verify the current plan and briefing in VISTA before flight.",9);y-=14;
  foreach(var section in sections)
  {
   if(y<115)NewPage();current.Append("0.15 0.37 0.48 rg\n");Paragraph(section.Title.ToUpperInvariant(),11,true);current.Append("0.08 0.13 0.20 rg\n");y-=3;Paragraph(section.Text);y-=16;
  }
  Text($"VISTA / Simulator exercise briefing / Page {page}",44,25,8);pages.Add(current.ToString());
  var objects=new List<byte[]>();void Obj(string t)=>objects.Add(Encoding.Latin1.GetBytes(t));
  Obj("<< /Type /Catalog /Pages 2 0 R >>");Obj($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ",Enumerable.Range(0,pages.Count).Select(i=>$"{5+i*2} 0 R"))}] >>");
  Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
  for(int i=0;i<pages.Count;i++){Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6+i*2} 0 R >>");var bytes=Encoding.Latin1.GetBytes(pages[i]);Obj($"<< /Length {bytes.Length} >>\nstream\n{pages[i]}endstream");}
  using var output=new MemoryStream();void Raw(string t){var b=Encoding.Latin1.GetBytes(t);output.Write(b);}Raw("%PDF-1.4\n%VISTA\n");var offsets=new List<long>{0};for(int i=0;i<objects.Count;i++){offsets.Add(output.Position);Raw($"{i+1} 0 obj\n");output.Write(objects[i]);Raw("\nendobj\n");}
  var xref=output.Position;Raw($"xref\n0 {objects.Count+1}\n0000000000 65535 f \n");foreach(var offset in offsets.Skip(1))Raw(offset.ToString("0000000000",CultureInfo.InvariantCulture)+" 00000 n \n");Raw($"trailer\n<< /Size {objects.Count+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
  File.WriteAllBytes(path,output.ToArray());
 }
}
public sealed partial class MainViewModel
{
 public void ExportBriefingPdf(string path)
 {
  if(briefingMission is null||NotesDirty)throw new InvalidOperationException("Open a briefing and save its notes before exporting.");
  var sections=new List<BriefingPdfSection>{new("Mission and aircraft",BriefingOverview),new("Acknowledgement",BriefingSignature),new("Task sequence",BriefingTasks),new("Mission instructions",BriefingInstructions),new("Additional mission notes",string.IsNullOrWhiteSpace(MissionNotes)?"None supplied":MissionNotes),new("Fuel plan / kg",BriefingFuelBreakdown),new("Loading / kg",BriefingWeights),new("Timing and operational information",BriefingDetails+"\n"+BriefingOperational)};
  sections.Add(new("Ordered mission waypoints",BriefingPoints.Count==0?"Direct route / no intermediate task points":string.Join("\n\n",BriefingPoints.Select(w=>$"{w.Position}. {w.Identifier} / {w.Latitude:F6}, {w.Longitude:F6}\nAltitude: {(w.AltitudeFt is int alt?alt+" ft":"Pilot discretion")} / Speed: {(w.SpeedKts is int speed?speed+" kt":"Pilot discretion")}\n{w.Instructions}"))));
  sections.Add(new("SimBrief route",briefing?.Route??"SimBrief briefing not imported"));sections.Add(new("Weather snapshot",BriefingWeather));
  BriefingPdf.Write(path,BriefingTitle,BriefingState+" / "+(briefingRecord?.IsCurrent==true?"Current imported plan":"Draft or out-of-date import"),sections);Message="Briefing PDF exported.";
 }
}
