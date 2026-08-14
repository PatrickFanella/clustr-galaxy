using System;

namespace ClustrGalaxy.Gameplay
{
    public sealed class SurveyEventBus
    {
        public event Action<SurveyEvent> Published = delegate { };
        public void Publish(SurveyEvent surveyEvent)
        {
            if (surveyEvent != null) Published(surveyEvent);
        }
    }
}
