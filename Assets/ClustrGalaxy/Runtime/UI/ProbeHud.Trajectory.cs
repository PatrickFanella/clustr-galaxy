using System;
using UnityEngine;
using UnityEngine.UI;

namespace ClustrGalaxy.UI
{
    public sealed partial class ProbeHud
    {
        private GameObject trajectoryPanel;
        private Text trajectoryStatus, radialDirection;
        private Text altitudeUnits, periapsisUnits, apoapsisUnits, stoppingUnits;
        private readonly Text[] altitudeReadout=new Text[6], radialReadout=new Text[4], stoppingReadout=new Text[6],
            periapsisReadout=new Text[6], apoapsisReadout=new Text[6], periodReadout=new Text[6];
        private RectTransform progradeMarker, retrogradeMarker;
        private void BuildTrajectoryDisplay()
        {
            trajectoryPanel=new GameObject("Trajectory instruments",typeof(RectTransform));
            trajectoryPanel.transform.SetParent(flightDisplay.transform,false);
            var root=trajectoryPanel.GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            DeckLine(root,"Instrument backing",48,118,272,315,new Color(.015f,.025f,.036f,.65f));
            trajectoryStatus=TrajectoryText("TWO-BODY FLIGHT ESTIMATE",61,129,245,11);
            altitudeUnits=TrajectoryRow("ALTITUDE / m",163,altitudeReadout);
            TrajectoryRow("RADIAL / m/s",205,radialReadout);radialDirection=TrajectoryText("+",164,218,18,15);
            stoppingUnits=TrajectoryRow("CLOSING STOP / m",247,stoppingReadout);
            periapsisUnits=TrajectoryRow("PERIAPSIS ALT / m",289,periapsisReadout);
            apoapsisUnits=TrajectoryRow("APOAPSIS ALT / m",331,apoapsisReadout);
            TrajectoryRow("ORBIT PERIOD / s",373,periodReadout);
            progradeMarker=TrajectoryText("PRO",0,0,40,11).rectTransform;
            retrogradeMarker=TrajectoryText("RET",0,0,40,11).rectTransform;
        }
        private Text TrajectoryText(string text,float x,float y,float width,int size)
        {
            var label=DeckText("Trajectory "+text,text,x,y,width,size,new Color(.55f,.82f,.86f));
            label.transform.SetParent(trajectoryPanel.transform,false);return label;
        }
        private Text TrajectoryRow(string caption,float y,Text[] digits)
        {
            var label=TrajectoryText(caption,61,y,235,10);
            for(var i=0;i<digits.Length;i++)digits[i]=TrajectoryText("0",184+i*16,y+13,18,16);
            return label;
        }
        private static void TrajectoryDistance(Text[] digits,Text label,double value,string metres,string kilometres)
        {
            if(!double.IsFinite(value)){for(var i=0;i<digits.Length;i++)if(digits[i].text!="-")digits[i].text="-";return;}
            var scaled=value>=1000000;var caption=scaled?kilometres:metres;if(label.text!=caption)label.text=caption;
            UpdateDigits(digits,(int)Math.Clamp(Math.Round(Math.Max(0,value)/(scaled?1000:1)),0,999999));
        }
        private void UpdateTrajectoryDisplay()
        {
            var telemetry=flight.Simulation.Telemetry;
            if(trajectoryPanel.activeSelf!=telemetry.Available)trajectoryPanel.SetActive(telemetry.Available);
            if(!telemetry.Available)return;
            var status=telemetry.Impact?"COASTING PATH · SURFACE RISK":telemetry.Escaping?"COASTING PATH · ESCAPE":"COASTING ORBIT · ESTIMATE";
            if(trajectoryStatus.text!=status)trajectoryStatus.text=status;
            TrajectoryDistance(altitudeReadout,altitudeUnits,telemetry.Altitude,"ALTITUDE / m","ALTITUDE / km");
            TrajectoryDistance(stoppingReadout,stoppingUnits,telemetry.StoppingDistance,"CLOSING STOP / m","CLOSING STOP / km");
            TrajectoryDistance(periapsisReadout,periapsisUnits,telemetry.Periapsis-telemetry.BodyRadius,"PERIAPSIS ALT / m","PERIAPSIS ALT / km");
            TrajectoryDistance(apoapsisReadout,apoapsisUnits,telemetry.Apoapsis-telemetry.BodyRadius,"APOAPSIS ALT / m","APOAPSIS ALT / km");
            UpdateDigits(radialReadout,(int)Math.Clamp(Math.Round(Math.Abs(telemetry.RadialSpeed)),0,9999));
            var direction=telemetry.RadialSpeed<0?"-":"+";if(radialDirection.text!=direction)radialDirection.text=direction;
            if(double.IsFinite(telemetry.Period))UpdateDigits(periodReadout,(int)Math.Clamp(Math.Round(telemetry.Period),0,999999));
            else for(var i=0;i<periodReadout.Length;i++)if(periodReadout[i].text!="-")periodReadout[i].text="-";
            var camera=targetingCamera!=null?targetingCamera:Camera.main;
            if(camera==null)return;
            var prograde=new Vector3(telemetry.Prograde.X,telemetry.Prograde.Y,telemetry.Prograde.Z);
            PlaceTrajectoryMarker(progradeMarker,camera,prograde);PlaceTrajectoryMarker(retrogradeMarker,camera,-prograde);
        }
        private static void PlaceTrajectoryMarker(RectTransform marker,Camera camera,Vector3 direction)
        {
            var point=camera.WorldToViewportPoint(camera.transform.position+direction*100);
            var visible=direction.sqrMagnitude>.5f&&point.z>0;
            if(marker.gameObject.activeSelf!=visible)marker.gameObject.SetActive(visible);
            if(visible)marker.anchoredPosition=new Vector2(Mathf.Clamp(point.x,.05f,.95f)*1280,-(1-Mathf.Clamp(point.y,.1f,.9f))*720);
        }
    }
}
