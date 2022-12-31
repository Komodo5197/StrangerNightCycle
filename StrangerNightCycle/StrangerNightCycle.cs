using OWML.Common;
using OWML.ModHelper;
using UnityEngine;

namespace StrangerNightCycle
{
    public class StrangerNightCycle : ModBehaviour
    {

        private RingWorldController _ringWorldController;
        private OWEmissiveRenderer _ringWorldBulb;
        private PlanetaryFogController _ringWorldFog;
        private OWTriggerVolume _ringWorldBulbHazard;
        private OWLight2 _ringWorldSun;
        private OWLight2 _ringWorldAmbient;
        private RingRiverController _ringRiverController;
        private GameObject _damGrates;
        private float _originalSunIntensity;
        private float _originalAmbientIntensity;
        private Color _originalBulbColor;
        private Color _originalFogColor;
        FloodToggle[] toggles;
        VisorRainEffectVolume[] rain;
        bool[] toggleState;
        RiverHazardToggle[] rapids;
        bool[] rapidsState;
        private bool _initialised;
        float daybreak = 80.0f;
        float ambient = 0.2f;
        private int _propID_InnerRadiusLow = Shader.PropertyToID("_InnerRadiusLow");
        private int _propID_InnerRadiusHigh = Shader.PropertyToID("_InnerRadiusHigh");
        private int _propID_InnerRadiusFinal = Shader.PropertyToID("_InnerRadiusFinal");
        private int _propID_FloodLerp = Shader.PropertyToID("_FloodLerp");
        private int _propID_WaveDeltaDegrees = Shader.PropertyToID("_WaveDeltaDegrees");
        // Dam break time: 780
        // flicker time: sail 400 + light 401 + depart 405 + dam 411
        private bool flooding = false;
        private bool floodComplete = false;
        bool firstUpdate = true;
        float floodStart = 50.0f;
        float floodPhaseChange = 0.431f;

        private void Start()
        {
            _initialised = false;
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        private void Update()
        {
            if (!_initialised) return;
            if (firstUpdate)
            {
                firstUpdate = false;
                ResetRiver();
            }

            float sunPercentage = 1.0f / (1.0f + (float)Mathf.Exp(-(TimeLoop.GetSecondsElapsed() - daybreak) / 3.0f));
            float ambientPercentage = sunPercentage * (1.0f - ambient) + ambient;
            bool damBroken = _ringWorldController._riverController._updateFlood || _ringWorldController._riverController._floodComplete;

            _ringWorldSun.SetIntensity(sunPercentage * _originalSunIntensity);
            _ringWorldAmbient.SetIntensity(ambientPercentage * _originalAmbientIntensity);
            _ringWorldBulb.SetEmissionColor(_originalBulbColor * sunPercentage);
            _ringWorldFog.fogTint = _originalFogColor * ambientPercentage;
            _ringWorldBulbHazard.SetTriggerActivation(sunPercentage>0.25f);

            if(!flooding && !floodComplete && TimeLoop.GetSecondsElapsed() >= floodStart && !damBroken)
            {
                flooding = true;
                _damGrates.SetActive(true);
            }
            if (flooding)
            {
                float floodLerp = Mathf.InverseLerp(floodStart+1.0f, floodStart + 110.0f, TimeLoop.GetSecondsElapsed());
                if (floodLerp < floodPhaseChange)
                {
                    float segmentLerp = Mathf.InverseLerp(0.0f, floodPhaseChange, floodLerp);
                    float damLerp = Mathf.Lerp(270.0f, 290.0f, segmentLerp);
                    SetWaterState(segmentLerp, 330.0f, damLerp, 298.0f);
                    for(int i=0; i<toggles.Length;i++)
                    {
                        if (!toggleState[i] && CalcFloodTime(toggles[i]._floodSensor.transform.position) <= segmentLerp)
                        {
                            SetFloodToggle(toggles[i], true);
                            toggleState[i] = true;
                        }
                    }
                    for (int i = 0; i < rapids.Length; i++)
                    {
                        if (!rapidsState[i] && CalcFloodTime(rapids[i]._floodSensor.transform.position) <= segmentLerp)
                        {
                            SetHazzardToggle(rapids[i], true);
                            rapidsState[i] = true;
                        }
                    }
                }
                else if (floodLerp <= 0.9999f) {
                    float segmentLerp = Mathf.InverseLerp(floodPhaseChange, 1.0f, floodLerp);
                    float damLerp = Mathf.Lerp(297.0f,240.0f, segmentLerp);
                    float riverLerp = Mathf.Lerp(298.0f, 300.0f, segmentLerp);
                    float finalLerp = Mathf.Lerp(298.0f, 285.0f, segmentLerp);
                    SetWaterState(damBroken?1.0f:0.0f, riverLerp, damLerp, finalLerp);
                }
                else
                {
                    flooding = false;
                    ResetRiver();
                }
            }
            
        }

        private void OnCompleteSceneLoad(OWScene oldScene, OWScene newScene)
        {
            if (newScene == OWScene.SolarSystem)
            {
                _ringWorldController = FindObjectOfType<RingWorldController>();
                _ringWorldSun = GameObject.Find("RingWorld_Body/Sector_RingInterior/Lights_RingInterior/IP_SunLight").GetComponent<OWLight2>();
                _ringWorldAmbient = GameObject.Find("RingWorld_Body/Sector_RingInterior/Lights_RingInterior/AmbientLight_IP_Surface").GetComponent<OWLight2>();
                _ringWorldBulb = GameObject.Find("RingWorld_Body/Sector_RingInterior/Geometry_RingInterior/Structure_IP_ArtificialSun/ArtificialSun_Bulb").GetComponent<OWEmissiveRenderer>();
                _ringWorldFog = GameObject.Find("RingWorld_Body/Atmosphere_IP/FogSphere").GetComponent<PlanetaryFogController>();
                _ringWorldBulbHazard = GameObject.Find("RingWorld_Body/Sector_RingInterior/Interactibles_RingInterior/HazardVolume_ArtificialSun").GetComponent<OWTriggerVolume>();
                _ringRiverController = GameObject.Find("RingWorld_Body/Sector_RingInterior/Volumes_RingInterior/RingRiverFluidVolume").GetComponent<RingRiverController>();
                _damGrates = GameObject.Find("RingWorld_Body/Sector_RingInterior/Geometry_RingInterior/Dam_Root/Dam_Intact/DamGrateWater");
                toggles = GameObject.Find("RingWorld_Body").GetComponentsInChildren<FloodToggle>();
                rain = GameObject.Find("RingWorld_Body").GetComponentsInChildren<VisorRainEffectVolume>();
                rapids = GameObject.Find("RingWorld_Body").GetComponentsInChildren<RiverHazardToggle>();

                _originalSunIntensity = _ringWorldSun.GetIntensity();
                _originalAmbientIntensity = _ringWorldAmbient.GetIntensity();
                _originalBulbColor = _ringWorldBulb.GetOriginalEmissionColor();
                _originalFogColor = _ringWorldFog.fogTint;

                _initialised = true;
                Configure(ModHelper.Config);
            }
            else
            {
                _initialised = false;
            }
        }

        public override void Configure(IModConfig config)
        {
            bool enableDarkness = config.GetSettingsValue<bool>("Enable Darkness");
            bool enableLowWater = config.GetSettingsValue<bool>("Enable Low Water");
            daybreak = enableDarkness?config.GetSettingsValue<float>("Daybreak"):-500.0f;
            floodStart = enableLowWater?config.GetSettingsValue<float>("Daybreak") - 70.0f:-500.0f;
            float departure = config.GetSettingsValue<float>("Departure");
            float damCollapse = config.GetSettingsValue<float>("Dam Collapse");

            if (!_initialised) return;
            _ringWorldController._sailDeployTime = departure;
            _ringWorldController._lightFlickerTime = departure+1.0f;
            _ringWorldController._departTime = departure+5.0f;
            _ringWorldController._damDamageTime = departure+10.0f;
            _ringWorldController._damBreakTime = damCollapse;
            ResetRiver();
        }

        private void ResetRiver()
        {
            if(TimeLoop.GetSecondsElapsed() < floodStart)
            {
                flooding = false;
            }
            if(flooding || _ringWorldController._riverController._updateFlood || _ringWorldController._riverController._floodComplete)
            {
                return;
            }
            float targetLow;
            float targetHigh;
            toggleState = new bool[toggles.Length];
            rapidsState = new bool[rapids.Length];
            if (TimeLoop.GetSecondsElapsed()< floodStart)
            {
                targetLow = 330.0f;
                targetHigh = 270.0f;
                floodComplete = false;
                foreach (var item in rapids)
                {
                    SetHazzardToggle(item, false);
                }
                foreach (var item in toggles)
                {
                    SetFloodToggle(item, false);
                }
                _damGrates.SetActive(false);
            }
            else
            {
                targetLow = 300.0f;
                targetHigh = 240.0f;
                floodComplete = true;
                foreach(var item in rapids)
                {
                    SetHazzardToggle(item, true);
                }
                foreach(var item in toggles)
                {
                    SetFloodToggle(item, true);
                }
                _damGrates.SetActive(true);
            }
            // Default river values:
            // inner final 285
            // inner high 240
            // inner low 300
            // outer radius 330
            // ramp start 360
            // ramp end 310
            SetWaterState(0.0f, targetLow, targetHigh, 285.0f);
        }

        private void SetWaterState(float floodLerp, float low, float high, float final)
        {
            _ringRiverController._riverCollider.SetFloodLerp(floodLerp);
            _ringRiverController._waveAudio.SetFloodLerp(floodLerp);
            _ringRiverController._riverCollider._innerRadiusLow = low;
            _ringRiverController._riverCollider._innerRadiusHigh = high;
            _ringRiverController._riverCollider._innerRadiusFinal = final;

            for (int i = 0; i < _ringRiverController._river.sharedMaterials.Length; i++)
            {
                if (_ringRiverController._river.sharedMaterials[i] != null)
                {
                    _ringRiverController._river.sharedMaterials[i].SetFloat(_propID_InnerRadiusLow, low);
                    _ringRiverController._river.sharedMaterials[i].SetFloat(_propID_InnerRadiusHigh, high);
                    _ringRiverController._river.sharedMaterials[i].SetFloat(_propID_InnerRadiusFinal, final);
                    _ringRiverController._riverMaterials[i].SetFloat(_propID_FloodLerp, floodLerp);
                }
            }
        }
        private float CalcFloodTime(Vector3 location)
        {
            Vector3 vector = _ringRiverController.transform.InverseTransformPoint(location);
            float num = OWMath.Angle(Vector3.forward, new Vector3(vector.x, 0f, vector.z), Vector3.up);
            if (num < 0f)
            {
                num += 360f;
            }
            return num / 360f;
        }

        private void SetHazzardToggle(RiverHazardToggle toggle, bool state)
        {
            if (toggle._activePostFlood) return;
            toggle._active = state;
            for (int j = 0; j < toggle._triggerVolumes.Length; j++)
            {
                toggle._triggerVolumes[j].SetTriggerActivation(state);
            }
            for (int k = 0; k < toggle._gameObjects.Length; k++)
            {
                toggle._gameObjects[k].SetActive(state);
            }
            toggle.UpdateEffects();
        }

        private void SetFloodToggle(FloodToggle toggle, bool state)
        {
            if (!toggle._deactivateOnFlood) return;
            
            for (int j = 0; j < toggle._volumes.Length; j++)
            {
                for(int i = 0; i < rain.Length; i++)
                {
                    if (rain[i]._triggerVolume== toggle._volumes[j])
                    {
                        toggle._volumes[j].SetTriggerActivation(state);
                    }
                }
            }
        }
    }
}