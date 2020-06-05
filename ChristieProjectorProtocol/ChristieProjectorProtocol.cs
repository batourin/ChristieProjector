using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
namespace Crestron.RAD.Drivers.Displays
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;                   // For parsing console responseList

    using Crestron.RAD.Common.BasicDriver;
    using Crestron.RAD.Common.Enums;
    using Crestron.RAD.Common.Transports;
    using Crestron.RAD.DeviceTypes.Display;
    using System.Collections.Generic;

    public class ChristieProjectorProtocol : ADisplayProtocol
    {
        private const string _standbyResponse = "000";
        private const string _powerOnResponse = "001";
        private const string _coolDownResponse = "010";
        private const string _warmingUpResponse = "011";

        private bool _boxerDialect = false;
        private uint[] _lampHours;


        // A prefix and suffix that is required for all messages sent to the device 
        private const char _messagePrefix = '(';
        private const char _messageSuffix = ')';

        public ChristieProjectorProtocol(ISerialTransport transportDriver, byte id)
            : base(transportDriver, id)
        {

            ResponseValidation = new ResponseValidator(Id, ValidatedData, this);
            ValidatedData.PowerOnPollingSequence = new[] 
            { 
                StandardCommandsEnum.VideoMutePoll, 
                StandardCommandsEnum.LampHoursPoll, 
                StandardCommandsEnum.InputPoll
            };
        }

        // This will be called when the transport indicates a change to the connection 
        protected override void ConnectionChanged(bool connection)
        {
            if (connection)
            {
                //Code to identify projector type...
                Log("Christie: Connection changed, checking projector's dialect.");
                Transport.Send("(SST+VERS?000)", null);
                //LampHoursPoll();
                Transport.Send("(HIS?)", null);
                //Transport.Send(ValidatedData.CommandsDictionary[StandardCommandsEnum.LampHoursPoll].StandardCommand, null);
            }
            
            base.ConnectionChanged(connection);
        }

        protected override bool PrepareStringThenSend(CommandSet commandSet)
        {
            // all responseList have to be bracketed with ( and ) to being sent out
            if (commandSet.CommandPrepared == false)
            {
                commandSet.Command = string.Format("{0}{1}{2}", _messagePrefix, commandSet.Command, _messageSuffix);
            }
            return base.PrepareStringThenSend(commandSet);
        }

        protected override void DeConstructPower(string response)
        {
            switch (response)
            {
                case _warmingUpResponse:
                    Log("DeconstructPower:Christie is Warming up");
                    FireEvent(DisplayStateObjects.WarmingUp, true);
                    break;
                case _coolDownResponse:
                    Log("DeconstructPower:Christie is Cooling down");
                    FireEvent(DisplayStateObjects.CoolingDown, true);
                    break;
                case _standbyResponse:
                    Log("DeconstructPower:Christie is in Standby, Powered Off");
                    response = ValidatedData.PowerFeedback.Feedback[StandardFeedback.PowerStatesFeedback.Off];
                    base.DeConstructPower(response);
                    break;
                case _powerOnResponse:
                    Log("DeconstructPower:Christie is Powered On");
                    response = ValidatedData.PowerFeedback.Feedback[StandardFeedback.PowerStatesFeedback.On];
                    base.DeConstructPower(response);
                    break;
            }
        }

        // Christie Projectors have multiple Lamps. 
        protected override void DeConstructLampHours(string response)
        {

            /*
             * 
             * HD-14K
             * RegEx: ^HIS!(?<id>\d{3})\s(?<number>\d{3})\s\"(?<sn>[\w|\d|/]+)\".+\"(?<hours>\d+):(?<minutes>\d{2})\"
             * HIS!000 001 "CUG1529" "21:34:23 2017/10/30" "N/A" "N/A" 015 002 "24:30"
             * HIS!001 002 "CUG1500" "21:34:23 2017/10/30" "N/A" "N/A" 015 002 "24:30"
             * HIS!002 001 "N/A" "21:34:16 2017/10/30" "21:34:23 2017/10/30" "N/A" 000 000 "00:00"
             * HIS!003 002 "N/A" "21:34:17 2017/10/30" "21:34:23 2017/10/30" "N/A" 000 000 "00:00"
             * 
             * Boxer-2K
             * RegEx: ^HIS!(?<id>\d+)\s\".+\"\s\"(?<sn>[\d|\w|/]+)\".*\"\s\d{4}\s\d{4}\s\d{4}\s\d{4}\s(?<hours>\d{4})
             * HIS!0000 "2018/04/23 16:14:01" "CUD2436" "" 0131 0000 0000 0000 2344
             * HIS!0001 "2019/02/22 13:41:35" "CVF0981" "" 0051 0000 0000 0000 0528
             * HIS!0002 "2018/04/23 16:14:03" "CUD2589" "" 0127 0000 0000 0000 2342
             * HIS!0003 "2018/04/23 16:14:04" "CUD2052" "" 0128 0000 0000 0000 2342
             * HIS!0004 "2018/04/23 16:14:02" "CUD2524" "" 0078 0001 0001 0000 1813
             * 
            */
            List<string> responseList = response.Split(new char[] {'\r','\n'}).Where(l => !string.IsNullOrEmpty(l)).ToList();

            foreach (string c in responseList)
            {
                Log(string.Format("Christie: DeconstructLamp:{0}", c));
            }

            const string re_hd14k = @"^HIS!(?<id>\d{3})\s(?<number>\d{3})\s\""(?<sn>[\w|\d|/]+)\"".+\""(?<hours>\d+):(?<minutes>\d{2})\""";
            const string re_boxer2k = @"^HIS!(?<id>\d+)\s\"".+\""\s\""(?<sn>[\d|\w|/]+)\"".*""\""\s\d{4}\s\d{4}\s\d{4}\s\d{4}\s(?<hours>\d{4})";

            int index = -1;
            uint hours = 0;
            bool fireEvent = false;

            foreach (string responseItem in responseList)
            {
                if (_boxerDialect)
                {
                    MatchCollection mc = Regex.Matches(responseItem, re_boxer2k);
                    if (mc.Count > 0)
                    {
                        if (mc[0].Groups["sn"].Value != "N/A")
                        {
                            try
                            {
                                index = Convert.ToInt32(mc[0].Groups["id"].Value);
                                hours = Convert.ToUInt32(mc[0].Groups["hours"].Value);
                                if (index >= 0 && index < _lampHours.Length && hours != _lampHours[index])
                                {
                                    _lampHours[index] = hours;
                                    fireEvent = true;
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }
                else
                {
                    MatchCollection mc = Regex.Matches(responseItem, re_hd14k);
                    if (mc.Count > 0)
                    {
                        if (mc[0].Groups["sn"].Value != "N/A")
                        {
                            try
                            {
                                index = Convert.ToInt32(mc[0].Groups["number"].Value) - 1;
                                hours = Convert.ToUInt32(mc[0].Groups["hours"].Value);
                                if (index >= 0 && index < _lampHours.Length && hours != _lampHours[index])
                                {
                                    _lampHours[index] = hours;
                                    fireEvent = true;
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }

                }
            }//foreach

            if (fireEvent)
            {
                var p = new Crestron.RAD.DeviceTypes.Display.Projector() {
                    LampHours = new List<uint>(_lampHours)
                };
                FireEvent(DisplayStateObjects.LampHours, p);
            }
        }

        public class ResponseValidator : ResponseValidation
        {
            private ChristieProjectorProtocol _protocol;
            private const string _responsePrefix = "\x28"; // "("
            private const string _responseSuffix = "\x29"; // ")"

            private const string _header = "\x28"; // "("
            private const string _delimiter = "\x29"; // ")"


            public ResponseValidator(byte id, DataValidation dataValidation, ChristieProjectorProtocol protocol)
                : base(id, dataValidation)
            {
                Id = id;
                DataValidation = dataValidation;
                _protocol = protocol;
            }

            public override ValidatedRxData ValidateResponse(string response, CommonCommandGroupType commandGroup)
            {
                ValidatedRxData validatedData = new ValidatedRxData(false, string.Empty);

                _protocol.Log(string.Format("Christie:Group:{0} Validate:{1}", commandGroup, response));

                List<string> responseList = response.Split(new char[] { '(', ')' }).Where(l => !string.IsNullOrEmpty(l)).ToList();
                foreach (string c in responseList)
                {
                    _protocol.Log(string.Format("Christie: Split:{0}", c));
                }

                if (responseList.FirstOrDefault() == "$")
                {
                    validatedData.CommandGroup = CommonCommandGroupType.AckNak;
                    validatedData.Data = DataValidation.AckDefinition;
                    validatedData.Ready = true;

                    return validatedData;
                }
                else if (responseList.FirstOrDefault() == "^")
                {
                    validatedData.CommandGroup = CommonCommandGroupType.AckNak;
                    validatedData.Data = DataValidation.NakDefinition;
                    validatedData.Ready = true;

                    return validatedData;
                }


                foreach (string responseItem in responseList)
                {
                    //_protocol.Log(string.Format("Christie:Processing:{0}", responseItem));
                    if (responseItem.StartsWith(DataValidation.PowerFeedback.GroupHeader))
                    {
                        validatedData.CommandGroup = CommonCommandGroupType.Power;
                        validatedData.Data = responseItem.Substring(DataValidation.PowerFeedback.GroupHeader.Length, 3);
                        validatedData.Ready = true;
                        break;
                    }
                    else if (responseItem.StartsWith(DataValidation.VideoMuteFeedback.GroupHeader))
                    {
                        validatedData.CommandGroup = CommonCommandGroupType.VideoMute;
                        validatedData.Data = responseItem.Substring(DataValidation.VideoMuteFeedback.GroupHeader.Length, 3);
                        validatedData.Ready = true;
                        break;
                    }
                    else if (responseItem.StartsWith(DataValidation.InputFeedback.GroupHeader))
                    {
                        validatedData.CommandGroup = CommonCommandGroupType.Input;
                        validatedData.Data = responseItem.Substring(DataValidation.VideoMuteFeedback.GroupHeader.Length, 3);
                        validatedData.Ready = true;
                        break;
                    }
                    else if (responseItem.StartsWith(DataValidation.LampHourFeedback.GroupHeader))
                    {
                        validatedData.CommandGroup = CommonCommandGroupType.LampHours;
                        validatedData.Data += responseItem + CrestronEnvironment.NewLine;
                        validatedData.Ready = true;
                        continue;
                    }
                    else if (responseItem.StartsWith("SST+VERS!000"))
                    {
                        if (responseItem.Contains("Boxer"))
                        {
                            _protocol.Log("Christie: dialect is Boxer.");
                            _protocol._boxerDialect = true;
                            _protocol._lampHours = new uint[4];
                            // remove StandardCommandsEnum.VideoMutePoll from polling sequence as no command found for Boxer2k
                            _protocol.ValidatedData.PowerOnPollingSequence = _protocol.ValidatedData.PowerOnPollingSequence.Where(item => item != StandardCommandsEnum.VideoMutePoll).ToArray();
                        }
                        else
                        {
                            _protocol.Log("Christie: dialect is HD-MD14K.");
                            _protocol._lampHours = new uint[2];
                        }
                        validatedData.CommandGroup = CommonCommandGroupType.AckNak;
                        validatedData.Data = DataValidation.AckDefinition;
                        validatedData.Ready = true;
                        break;
                    }
                    else if (!validatedData.Ready && responseItem.StartsWith("65535 00000 FYI"))
                    {
                        validatedData.CommandGroup = CommonCommandGroupType.AckNak;
                        validatedData.Data = DataValidation.AckDefinition;
                        validatedData.Ready = true;
                        continue;
                    }
                    else if (!validatedData.Ready && responseItem.StartsWith("65535 00000 ERR"))
                    {
                        validatedData.CommandGroup = CommonCommandGroupType.AckNak;
                        validatedData.Data = DataValidation.NakDefinition;
                        validatedData.Ready = true;
                        continue;
                    }
                    //_protocol.Log(string.Format("Christie:Processed:{0}", responseItem));
                }

                //_protocol.Log(string.Format("Christie: Validated Data {0}", validatedData.Data));
                return validatedData;
            }
        }
    }
}
