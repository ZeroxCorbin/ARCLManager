﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;

namespace ARCLTypes
{

    public class SyncStateEventArgs
    {
        public SyncStates State { get; set; } = SyncStates.WAIT;
        public string Message { get; set; } = "";
    }

    public class ReadOnlyConcurrentDictionary<TKey, TValue> : ConcurrentDictionary<TKey, TValue>
    {
        public bool Locked { get; set; } = true;
        public ReadOnlyConcurrentDictionary(int concurrencyLevel, int capacity) : base(concurrencyLevel, capacity) { }
        public new bool TryAdd(TKey key, TValue value)
        {
            if(Locked) return false;
            Locked = true;
            return base.TryAdd(key, value);
        }
        public new bool TryRemove(TKey key, out TValue value)
        {
            value = default;
            if(Locked) return false;
            Locked = true;
            return base.TryRemove(key, out value);
        }
    }

    public enum ARCLStatus
    {
        Pending,
        Available,
        AvailableForJobs,
        Interrupted,
        InProgress,
        Completed,
        Cancelling,
        Cancelled,
        BeforeModify,
        InterruptedByModify,
        AfterModify,
        UnAvailable,
        Failed,
        Loading
    }
    public enum ARCLSubStatus
    {
        None,
        AssignedRobotOffLine,
        NoMatchingRobotForLinkedJob,
        NoMatchingRobotForOtherSegment,
        NoMatchingRobot,
        ID_PICKUP,
        ID_DROPOFF,
        Available,
        AvailableForJobs,
        Parking,
        Parked,
        DockParking,
        DockParked,
        UnAllocated,
        Allocated,
        BeforePickup,
        BeforeDropoff,
        BeforeEvery,
        Before,
        Buffering,
        Buffered,
        Driving,
        After,
        AfterEvery,
        AfterPickup,
        AfterDropoff,
        NotUsingEnterpriseManager,
        UnknownBatteryType,
        ForcedDocked,
        Lost,
        EStopPressed,
        Interrupted,
        InterruptedButNotYetIdle,
        OutgoingARCLConnLost,
        ModeIsLocked,
        Cancelled_by_MobilePlanner,
        CustomUser
    }

    /// <summary>
    /// The state of the Managers dictionary.
    /// State= WAIT; Wait to access the dictionary.
    ///              Calling Start() or Stop() sets this state.
    /// State= DELAYED; The dictionary Values are not valid.
    ///                 The dictionary Values being updated from the ARCL Server are delayed.
    /// State= UPDATING; The dictionary Values are being updated.
    /// State= OK; The dictionary is up to date.
    /// </summary>
    public enum SyncStates
    {
        WAIT = -1,
        DELAYED,
        UPDATING,
        OK
    }
    public class ARCLCommands
    {
        public Dictionary<string, string> ARCLCommands_Help_4_10_1 { get; } = new Dictionary<string, string>()
        {
            {"addCustomCommand","Adds a custom command that'll send a message out ARCL when called"},
{"addCustomStringCommand","Adds a custom string command that'll send a message out ARCL when called"},
{"analogInputList","Lists the named analog inputs"},
{"analogInputQueryRaw","Queries the state of an analog input by raw"},
{"analogInputQueryVoltage","Queries the state of an analog input by voltage"},
{"applicationBlockDrivingClear","Clears an application blockDriving [abdc]"},
{"applicationBlockDrivingSet","Sets an application blockDriving [abds]"},
{"applicationFaultClear","Clears an application fault [afc]"},
{"applicationFaultQuery","Gets the list of any application faults currently triggered [afq]"},
{"applicationFaultSet","Sets an application fault [afs]"},
{"arclSendText","Sends the given message to all ARCL clients"},
{"centralServer","Gets information about the central server connection"},
{"configAdd","Adds a config file line to the config values being imported."},
{"configParse","Parses the imported config values and terminates the configStart command."},
{"configStart","Starts importing config values. Use with configAdd and configParse."},
{"connectOutgoing","(re)connects a socket to the given outside server"},
{"createInfo","creates a piece of information"},
{"dock","Sends the robot to the dock"},
{"doTask","does a task"},
{"doTaskInstant","does an instant task (doesn't interrupt modes)"},
{"echo","with no args gets echo, with args sets echo"},
{"enableMotors","Enables the motors so the robot can drive again"},
{"engageCancel","cancel engage state (without disengaging)"},
{"engageQueryState","query the engage state of a robot"},
{"executeMacro","executes a given macro"},
{"executeMacroTemplate","executes a specified macro template with the given parameters"},
{"extIOAdd","Adds the external digital inputs and outputs [eda]"},
{"extIODump","Dumps the external inputs and outputs [edd]"},
{"extIODumpLocal","Dumps the external inputs and outputs locally for the robot [eddl]"},
{"extIOInputUpdate","Updates the external digital inputs [ediu]"},
{"extIOInputUpdateBit","Updates an external digital input bit, e.g. bit 32 most significant bit, bit 1 least [edib]"},
{"extIOInputUpdateByte","Updates an external digital input byte, e.g. byte 4 in a 32-bit bank is most significant [edi8]"},
{"extIOOutputUpdate","Updates the external digital outputs [edou]"},
{"extIOOutputUpdateBit","Updates an external digital output bit, e.g. bit 32 most significant bit, bit 1 least [edob]"},
{"extIOOutputUpdateByte","Updates an external digital output byte, e.g. byte 4 in a 32-bit bank is most significant [edo8]"},
{"extIORemove","Removes the external digital inputs and outputs [edr]"},
{"faultsGet","Gets the list of any faults currently triggered [fg]"},
{"getConfigSectionInfo","Gets the info about a section of the config"},
{"getConfigSectionList","Gets the list of sections in the config"},
{"getConfigSectionValues","Gets the values in a section of the config"},
{"getDataStoreFieldInfo","Gets the info on a field in the Data Store [dsfi]"},
{"getDataStoreFieldList","Gets the list of field names in the Data Store [dsfl]"},
{"getDataStoreFieldValues","Gets the values of a field in the Data Store [dsfv]"},
{"getDataStoreGroupInfo","Gets the info on a group in the Data Store [dsgi]"},
{"getDataStoreGroupList","Gets the list of groups in the Data Store [dsgl]"},
{"getDataStoreGroupValues","Gets the values of a group in the Data Store [dsgv]"},
{"getDataStoreTripGroupList","Gets the list of groups with trip values in the Data Store [dstgl]"},
{"getDateTime","gets the date and time"},
{"getGoals","gets a list of goals in the map (for use with goto)"},
{"getInfo","Gets a piece of information"},
{"getInfoList","Gets the list of info available from 'getInfo'"},
{"getMacros","gets a list of macros in the map (for use with executeMacro)"},
{"getMacroTemplates","gets a list of macro templates in the map (for use with executeMacroTemplates)"},
{"getPoseEncoder","Gets the encoder pose of the robot"},
{"getRoutes","gets the routes the robot has"},
{"go","Stops the robot from any wait it is doing then resume patrolling or makes the robot idle 2 minutes so that it will resume doing queued jobs (with the right parameters).  See also stay."},
{"goalDistanceRemaining","Gets the distance remaining to robots goals"},
{"goalDistanceRemainingLocal","Gets the distance remaining to this robot's goal"},
{"help","gives the listing of available commands (optionally with prefix)"},
{"inputList","Lists the named digital inputs [inL]"},
{"inputQuery","Queries the state of a named digital input [inQ]"},
{"localizeAtGoal","Localizes to a given goal [lag]"},
{"log","Logs the message to the normal log file"},
{"mapObjectInfo","Gets the information about a map object specified by name."},
{"mapObjectList","Gets a list the map objects of a given type."},
{"mapObjectTypeInfo","Gets information about a particular type of map object."},
{"mapObjectTypeList","Gets a list of the types of map objects in the map."},
{"mapObjectUpdate","Creates or updates a map object with the specified data."},
{"modeLock","Locks the current mode"},
{"modeQuery","Queries the current mode and it's lock status"},
{"modeUnlock","Unlocks the current mode"},
{"newConfigParam","Adds a new param to the config (shows up in MP)"},
{"newConfigParamEnd","Finishes adding multiple params to the config. Must be paired with a newConfigParamStart."},
{"newConfigParamStart","Starts adding multiple params to the config. Optional. If specified, must be followed by a newConfigParamEnd."},
{"newConfigSectionComment","Adds a comment to a section (shows up in MP)"},
{"odometer","Shows the robot trip odometer"},
{"odometerReset","Resets the robot trip odometer"},
{"oneLineStatus","Gets the status of the robot on one line"},
{"outputList","Lists the named outputs [outL]"},
{"outputOff","Turns off a named digital output [outOff]"},
{"outputOn","Turns on a named digital output [outOn]"},
{"outputQuery","Queries the state of a named output [outQ]"},
{"patrolOnce","Patrols the given route once, starts at optional index"},
{"patrolResume","Resumes the last patrol"},
{"pauseTaskCancel","Cancels the pause task (if it's active, causing it to succeed)"},
{"pauseTaskFail","Fails the pause task (if it's active)"},
{"pauseTaskState","Gets the state of the pause task"},
{"payloadQueryLocal","Queries the payload for this robot [pql]"},
{"payloadRemove","Empties a payload slot for this robot [pr]"},
{"payloadSet","Sets the payload for this robot [ps]"},
{"payloadSlotCountLocal","Queries for number of payload slots [pscl]"},
{"play","Plays the given wave file already on the robot"},
{"popupSimple","Creates a simple popup"},
{"queryDockStatus","Gets the docking status"},
{"queryFaults","Shows the faults of all the robots or a single robot [qf]"},
{"queryMotors","Queries the state of the motors"},
{"queueCancel","Cancels an item by type and value [qc]"},
{"queueCancelLocal","Cancels an item by type and value [qcl]"},
{"queueCancelMultiSegment","Cancels a multi goal job segment by id [qcms]"},
{"queueDropoff","Queues the robot to the dropoff goal [qd]"},
{"queueModify","Modifies a current or pending job segment [qmod]"},
{"queueModifyLocal","Modifies a current or pending job segment [qmodl]"},
{"queueMulti","Queues multiple goals for any appropriate robot to do [qm]"},
{"queuePickup","Queues a pickup goal for any appropriate robot [qp]"},
{"queuePickupDropoff","Queues a pickup dropoff goal pair for any appropriate robot to do [qpd]"},
{"queueQuery","Queries the queue by type and value [qq]"},
{"queueQueryLocal","Queries the queue by type and value [qql]"},
{"queueShow","Shows the Queue [qs]"},
{"queueShowCompleted","Shows the Queue Completed [qsc]"},
{"queueShowRobot","Shows the status of all the robots [qsr]"},
{"queueShowRobotLocal","Queries the status of the robot [qsrl]"},
{"quit","closes this connection to the server"},
{"say","Says the given string"},
{"shutdown","shuts down the robot"},
{"status","Gets the status of the robot"},
{"stay","Makes the robot wait for a minute, then resume patrol or become idle 2 minutes so that it will resume queued jobs (with the right parameters).  See also go."},
{"stop","Stops the robot"},
{"switchableForbiddenList","Gets the list and state of switchable forbiddens [sfL]"},
{"switchForbiddenOff","Switch a specific switchable forbidden off [sfOff]"},
{"switchForbiddenOn","Switch a specific switchable forbidden on [sfOn]"},
{"switchForbiddensOffByPrefix","Switch off switchable forbiddens by prefix [sfOffP]"},
{"switchForbiddensOnByPrefix","Switch on switchable forbiddens by prefix [sfOnP]"},
{"tripReset","Resets the trip values in the Data Store [tr]"},
{"undock","Undocks the robot (done automatically too)"},
{"updateInfo","updates a piece of information"},
{"waitTaskCancel","Cancels the wait task (if it's active, causing it to succeed)"},
{"waitTaskFail","Fails the wait task (if it's active)"},
{"waitTaskState","Gets the state of the wait task"},
        };
        public Dictionary<string, string> ARCLCommands_Help_5_4_1 { get; } = new Dictionary<string, string>()
        {
            {"addCustomCommand","Adds a custom command that'll send a message out ARCL when called"},
{"addCustomStringCommand","Adds a custom string command that'll send a message out ARCL when called"},
{"analogInputList","Lists the named analog inputs"},
{"analogInputQueryRaw","Queries the state of an analog input by raw"},
{"analogInputQueryVoltage","Queries the state of an analog input by voltage"},
{"applicationBlockDrivingClear","Clears an application blockDriving [abdc]"},
{"applicationBlockDrivingSet","Sets an application blockDriving [abds]"},
{"applicationFaultClear","Clears an application fault [afc]"},
{"applicationFaultQuery","Gets the list of any application faults currently triggered [afq]"},
{"applicationFaultSet","Sets an application fault [afs]"},
{"arclSendText","Sends the given message to all ARCL clients"},
{"centralServer","Gets information about the central server connection"},
{"connectOutgoing","(re)connects a socket to the given outside server"},
{"createInfo","creates a piece of information"},
{"dock","Sends the robot to the dock"},
{"doTask","does a task"},
{"doTaskInstant","does an instant task (doesn't interrupt modes)"},
{"echo","with no args gets echo, with args sets echo"},
{"enableMotors","Enables the motors so the robot can drive again"},
{"engageCancel","cancel engage state (without disengaging)"},
{"engageQueryState","query the engage state of a robot"},
{"executeMacro","executes a given macro"},
{"executeMacroTemplate","executes a specified macro template with the given parameters"},
{"faultsGet","Gets the list of any faults currently triggered [fg]"},
{"getConfigSectionInfo","Gets the info about a section of the config"},
{"getConfigSectionList","Gets the list of sections in the config"},
{"getConfigSectionValues","Gets the values in a section of the config"},
{"getDataStoreFieldInfo","Gets the info on a field in the Data Store [dsfi]"},
{"getDataStoreFieldList","Gets the list of field names in the Data Store [dsfl]"},
{"getDataStoreFieldValues","Gets the values of a field in the Data Store [dsfv]"},
{"getDataStoreGroupInfo","Gets the info on a group in the Data Store [dsgi]"},
{"getDataStoreGroupList","Gets the list of groups in the Data Store [dsgl]"},
{"getDataStoreGroupValues","Gets the values of a group in the Data Store [dsgv]"},
{"getDataStoreTripGroupList","Gets the list of groups with trip values in the Data Store [dstgl]"},
{"getDateTime","gets the date and time"},
{"getGoals","gets a list of goals in the map (for use with goto)"},
{"getInfo","Gets a piece of information"},
{"getInfoList","Gets the list of info available from 'getInfo'"},
{"getMacros","gets a list of macros in the map (for use with executeMacro)"},
{"getMacroTemplates","gets a list of macro templates in the map (for use with executeMacroTemplates)"},
{"getPoseEncoder","Gets the encoder pose of the robot"},
{"getRoutes","gets the routes the robot has"},
{"go","Stops the robot from any wait it is doing then resume patrolling or makes the robot idle 2 minutes so that it will resume doing queued jobs (with the right parameters).  See also stay."},
{"goalDistanceRemainingLocal","Gets the distance remaining to this robot's goal"},
{"help","gives the listing of available commands (optionally with prefix)"},
{"inputList","Lists the named digital inputs [inL]"},
{"inputQuery","Queries the state of a named digital input [inQ]"},
{"localizeAtGoal","Localizes to a given goal [lag]"},
{"log","Logs the message to the normal log file"},
{"mapObjectInfo","Gets the information about a map object specified by name."},
{"mapObjectList","Gets a list the map objects of a given type."},
{"mapObjectTypeInfo","Gets information about a particular type of map object."},
{"mapObjectTypeList","Gets a list of the types of map objects in the map."},
{"mapObjectUpdate","Creates or updates a map object with the specified data."},
{"modeLock","Locks the current mode"},
{"modeQuery","Queries the current mode and it's lock status"},
{"modeUnlock","Unlocks the current mode"},
{"multiRobotSizeClear","Clears the multirobot size back to default"},
{"multiRobotSizeSet","Sets the multirobot size"},
{"newConfigParam","Adds a new param to the config (shows up in MP)"},
{"newConfigParamEnd","Finishes adding multiple params to the config. Must be paired with a newConfigParamStart."},
{"newConfigParamStart","Starts adding multiple params to the config. Optional. If specified, must be followed by a newConfigParamEnd."},
{"newConfigSectionComment","Adds a comment to a section (shows up in MP)"},
{"odometer","Shows the robot trip odometer"},
{"odometerReset","Resets the robot trip odometer"},
{"oneLineStatus","Gets the status of the robot on one line"},
{"outputList","Lists the named outputs [outL]"},
{"outputOff","Turns off a named digital output [outOff]"},
{"outputOn","Turns on a named digital output [outOn]"},
{"outputQuery","Queries the state of a named output [outQ]"},
{"patrol","patrols the given route"},
{"patrolOnce","Patrols the given route once, starts at optional index"},
{"patrolResume","Resumes the last patrol"},
{"payloadQueryLocal","Queries the payload for this robot [pql]"},
{"payloadRemove","Empties a payload slot for this robot [pr]"},
{"payloadSet","Sets the payload for this robot [ps]"},
{"payloadSlotCountLocal","Queries for number of payload slots [pscl]"},
{"play","Plays the given wave file already on the robot"},
{"popupSimple","Creates a simple popup"},
{"queryDockStatus","Gets the docking status"},
{"queryMotors","Queries the state of the motors"},
{"queueCancelLocal","Cancels an item by type and value [qcl]"},
{"queueDropoff","Queues the robot to the dropoff goal [qd]"},
{"queueModifyLocal","Modifies a current or pending job segment [qmodl]"},
{"queueQueryLocal","Queries the queue by type and value [qql]"},
{"queueShowRobotLocal","Queries the status of the robot [qsrl]"},
{"quit","closes this connection to the server"},
{"say","Says the given string"},
{"shutdown","shuts down the robot"},
{"status","Gets the status of the robot"},
{"stay","Makes the robot wait for a minute, then resume patrol or become idle 2 minutes so that it will resume queued jobs (with the right parameters).  See also go."},
{"stop","Stops the robot"},
{"switchableForbiddenList","Gets the list and state of switchable forbiddens [sfL]"},
{"switchForbiddenOff","Switch a specific switchable forbidden off [sfOff]"},
{"switchForbiddenOn","Switch a specific switchable forbidden on [sfOn]"},
{"switchForbiddensOffByPrefix","Switch off switchable forbiddens by prefix [sfOffP]"},
{"switchForbiddensOnByPrefix","Switch on switchable forbiddens by prefix [sfOnP]"},
{"tripReset","Resets the trip values in the Data Store [tr]"},
{"undock","Undocks the robot (done automatically too)"},
{"updateInfo","updates a piece of information"},
{"waitTaskCancel","Cancels the wait task (if it's active, causing it to succeed)"},
{"waitTaskFail","Fails the wait task (if it's active)"},
{"waitTaskState","Gets the state of the wait task"},

        };
        public Dictionary<string, string> ARCLCommands_I617_E_02 { get; } = new Dictionary<string, string>()
        {
{"analogInputList","analogInputList"},
{"analogInputQueryRaw","analogInputQueryRaw <name>"},
{"analogInputQueryVoltage","analogInputQueryVoltage <name>"},
{"applicationFaultClear","applicationFaultClear <name>"},
{"applicationFaultQuery","applicationFaultQuery"},
{"applicationFaultSet","applicationFaultSet <name> \"<short_description>\" \"<long_description>\" <bool_driving> <bool_critical>"},
{"arclSendText","arclSendText <string>"},
{"configAdd","configAdd <section>configAdd <configuration> <value>"},
{"configParse","configParse"},
{"configStart","configStart"},
{"connectOutgoing","connectOutgoing <hostname> <port>"},
{"createInfo","createInfo <infoName> <maxLen> <infoValue>"},
{"dock","dock"},
{"doTask","doTask <task> <argument>"},
{"doTaskInstant","doTaskInstant <task> <argument>"},
{"echo","echo [state]"},
{"enableMotors","enableMotors"},
{"executeMacro","executeMacro <macro_name >"},
{"extIOAdd (shortcut: eda)","extIOAdd <name> <numInputs> <numOutputs>"},
{"extIODump (shortcut: edd)","extIODump"},
{"extIODumpLocal (shortcut: eddl)","extIODumpLocal"},
{"extIOInputUpdate (shortcut: ediu)","extIOInputUpdate <name> <valueInHexOrDec>"},
{"extIOInputUpdateBit (shortcut: edib)","extIOInputUpdateBit <name> <bit position> <0 or 1>"},
{"extIOInputUpdateByte (shortcut: edi8)","extIOInputUpdateByte <name> <byte position> <valueInHex>response."},
{"extIOOutputUpdate (shortcut: edou)","extIOOutputUpdate <name> <valueInHexOrDec>"},
{"extIOOutputUpdateBit (shortcut: edob)","extIOOutputUpdateBit <name> <bit position> <0 or 1>"},
{"extIOOutputUpdateByte (shortcut: edo8)","extIOOutputUpdateByte <name> <byte position> <valueInHex>response."},
{"extIORemove (shortcut: edr)","extIORemove <name>"},
{"faultsGet","faultsGet"},
{"getConfigSectionInfo","getConfigSectionInfo <section>"},
{"getConfigSectionList","getConfigSectionList"},
{"getConfigSectionValues","getConfigSectionValues <section>"},
{"getDataStoreFieldInfo (shortcut: dsfi)","getDataStoreFieldInfo <field>"},
{"getDataStoreFieldList (shortcut: dsfl)","getDataStoreFieldList"},
{"getDataStoreFieldValues (shortcut: dsfv)","getDataStoreFieldValues <field>"},
{"getDataStoreGroupInfo (shortcut: dsgi)","getDataStoreGroupInfo <group>"},
{"getDataStoreGroupList (shortcut: dsgl)","getDataStoreGroupList"},
{"getDataStoreGroupValues (shortcut: dsgv)","getDataStoreGroupValues <group>"},
{"getDataStoreTripGroupList (shortcut: dstgl)","getDataStoreTripGroupList"},
{"getDateTime","getDateTime"},
{"getGoals","getGoals"},
{"getInfo","getInfo <infoName>"},
{"getInfoList","getInfoList"},
{"getMacros","getMacros"},
{"getRoutes","getRoutes"},
{"help","help"},
{"inputList","inputList"},
{"inputQuery","inputQuery <name>"},
{"log","log <message> [level]"},
{"mapObjectInfo","mapObjectInfo <name>"},
{"mapObjectList","mapObjectList <type>"},
{"mapObjectTypeInfo","mapObjectTypeInfo <type>"},
{"mapObjectTypeList","mapObjectTypeList"},
{"newConfigParam","newConfigParam <section> <name> <description> <priority_level> <type> <default_value><min> <max> <DisplayHint>"},
{"newConfigSectionComment","newConfigSectionComment <section> <comment>"},
{"odometer","odometer"},
{"odometerReset","odometerReset"},
{"oneLineStatus","oneLineStatus"},
{"outputList","outputList"},
{"outputOff","outputOff <name>"},
{"outputOn","outputOn <name>"},
{"outputQuery","outputQuery <name>"},
{"patrol","patrol <route_name>"},
{"patrolOnce","patrolOnce <route_name> [index]"},
{"patrolResume","patrolResume <route_name>"},
{"payloadQuery (shortcut: pq)","payloadQuery [robotName or \"default\"] [slotNumber or \"default\"] [echoString]"},
{"payloadQueryLocal (shortcut: pql)","payloadQueryLocal [slotNumber or \"default\"] [echoString]"},
{"payloadRemove (shortcut: pr)","payloadRemove <slot_number>"},
{"payloadSet (shortcut: ps)","payloadSet <slot_number> <slot_string>"},
{"payloadSlotCount (shortcut: psc)","payloadSlotCount [robotName or \"default\"] [echoString]"},
{"payloadSlotCountLocal (shortcut: pscl)","payloadSlotCountLocal"},
{"play","play <path_file>"},
{"popupSimple","popupSimple <\"title\"> <\"message\"> <\"buttonLabel\"> <timeout>"},
{"queryDockStatus","queryDockStatus"},
{"queryFaults (shortcut: qf)","queryFaults [robotName or \"default\"] [echoString]"},
{"queryMotors","queryMotors"},
{"queueCancel (shortcut: qc)","queueCancel <type> <value> [echoString or \"default\"] [reason]"},
{"queueCancelLocal (shortcut: qcl)","queueCancelLocal <type> <value> [echoString] [reason]"},
{"queueDropoff (shortcut: qd)","queueDropoff <goalName> [priority] [jobId]"},
{"queueModify (shortcut: qmod)","queueModify <id> <type> <value>"},
{"queueModifyLocal (shortcut: qmodl)","queueModifyLocal <id> <type> <value>"},
{"queueMulti (shortcut: qm)","queueMulti <number of goals> <number of fields per goal> <goal<goal1 args> <goal2><goal2 args> … <goalN> <goalN args> [jobid]"},
{"queuePickup (shortcut: qp)","queuePickup <goalName> [priority or \"default\"] [jobId]"},
{"queuePickupDropoff (shortcut: qpd)","queuePickupDropoff <goal1Name> <goal2Name> [priority1 or \"default\"] [priority2 or\"default\"] [jobId]"},
{"queueQuery (shortcut: qq)","queueQuery <type> <value> [echoString]"},
{"queueQueryLocal (shortcut: qql)","queueQueryLocal <type> <value> [echoString]"},
{"queueShow (shortcut: qs)","queueShow [echoString]"},
{"queueShowCompleted (shortcut: qsc)","queueShowCompleted [echoString]"},
{"queueShowRobot (shortcut: qsr)","queueShowRobot [robotName or \"default\"] [echoString]"},
{"queueShowRobotLocal (shortcut: qsrl)","queueShowRobotLocal [echo_string]"},
{"quit","quit"},
{"say","say <text_string>"},
{"shutdown","shutdown"},
{"status","status"},
{"stop","stop"},
{"tripReset (shortcut: tr)","tripReset"},
{"undock","undock"},
{"updateInfo","updateInfo <infoName> <infoValue>"},
{"waitTaskCancel","waitTaskCancel"},
{"waitTaskState","waitTaskState"}

        };
        public Dictionary<string, string> ARCLCommands_I617_E_01 { get; } = new Dictionary<string, string>()
        {
            {"analogInputList","analogInputList"},
{"analogInputQueryRaw","analogInputQueryRaw <name>"},
{"analogInputQueryVoltage","analogInputQueryVoltage <name>"},
{"applicationFaultClear","applicationFaultClear <name>"},
{"applicationFaultQuery","applicationFaultQuery"},
{"applicationFaultSet","applicationFaultSet <name> \"<short_description>\" \"<long_description>\" <bool_driving> <bool_critical>"},
{"arclSendText","arclSendText <string>"},
{"configAdd","configadd <section>configadd <configuration> <value>"},
{"configParse","configParse"},
{"configStart","configstart"},
{"connectOutgoing","connectOutgoing <hostname> <port>"},
{"createInfo","createInfo <infoName> <maxLen> <infoValue>"},
{"dock","dock"},
{"doTask","doTask <task> <argument>"},
{"doTaskInstant","doTaskInstant <task> <argument>"},
{"echo","echo [state]"},
{"enableMotors","enableMotors"},
{"executeMacro","executeMacro <macro_name >"},
{"extIOAdd (shortcut: eda)","extIOAdd <name> <numInputs> <numOutputs>"},
{"extIODump (shortcut: edd)","extIODump"},
{"extIODumpLocal (shortcut: eddl)","extIODumpLocal"},
{"extIOInputUpdate (shortcut: ediu)","extIOInputUpdate <name> <valueInHexOrDec>"},
{"extIOInputUpdateBit (shortcut: edib)","extIOInputUpdateBit <name> <bit position> <0 or 1>"},
{"extIOInputUpdateByte (shortcut: edi8)","extIOInputUpdateByte <name> <byte position> <valueInHex>"},
{"extIOOutputUpdate (shortcut: edou)","extIOOutputUpdate <name> <valueInHexOrDec>"},
{"extIOOutputUpdateBit (shortcut: edob)","extIOOutputUpdateBit <name> <bit position> <0 or 1>"},
{"extIOOutputUpdateByte (shortcut: edo8)","extIOOutputUpdateByte <name> <byte position> <valueInHex>"},
{"extIORemove (shortcut: edr)","extIORemove <name>"},
{"faultsGet","faultsGet"},
{"getConfigSectionInfo","getConfigSectionInfo <section>"},
{"getConfigSectionList","getConfigSectionList"},
{"getConfigSectionValues","getConfigSectionValues <section>"},
{"getDataStoreFieldInfo (shortcut: dsfi)","getDataStoreFieldInfo <field>"},
{"getDataStoreFieldList (shortcut: dsfl)","getDataStoreFieldList"},
{"getDataStoreFieldValues (shortcut: dsfv)","getDataStoreFieldValues <field>"},
{"getDataStoreGroupInfo (shortcut: dsgi)","getDataStoreGroupInfo <group>"},
{"getDataStoreGroupList (shortcut: dsgl)","getDataStoreGroupList"},
{"getDataStoreGroupValues (shortcut: dsgv)","getDataStoreGroupValues <group>"},
{"getDataStoreTripGroupList (shortcut: dstgl)","getDataStoreTripGroupList"},
{"getDateTime","getDateTime"},
{"getGoals","getGoals"},
{"getInfo","getInfo <infoName>"},
{"getInfoList","getInfoList"},
{"getMacros","getmacros"},
{"getRoutes","getRoutes"},
{"help","help"},
{"inputList","inputList"},
{"inputQuery","inputQuery <name>"},
{"log","log <message> [level]"},
{"mapObjectInfo","mapObjectInfo <name>"},
{"mapObjectList","mapObjectList <type>"},
{"mapObjectTypeInfo","mapObjectTypeInfo <type>"},
{"mapObjectTypeList","mapObjectTypeList"},
{"newConfigParam","newConfigParam <section> <name> <description> <priority_level> <type> <default_value><min> <max> <DisplayHint>"},
{"newConfigSectionComment","newConfigSectionComment <section> <comment>"},
{"odometer","odometer"},
{"odometerReset","odometerReset"},
{"oneLineStatus","oneLineStatus"},
{"outputList","outputList"},
{"outputOff","outputOff <name>"},
{"outputOn","outputOn <name>"},
{"outputQuery","outputQuery <name>"},
{"patrol","patrol <route_name>"},
{"patrolOnce","patrolOnce <route_name> [index]"},
{"patrolResume","patrolResume <route_name>"},
{"payloadQuery (shortcut: pq)","payloadQuery [robotName or \"default\"] [slotNumber or \"default\"] [echoString]"},
{"payloadQueryLocal (shortcut: pql)","payloadQueryLocal [slotNumber or \"default\"] [echoString]"},
{"payloadRemove (shortcut: pr)","payloadRemove <slot_number>"},
{"payloadSet (shortcut: ps)","payloadSet <slot_number> <slot_string>"},
{"payloadSlotCount (shortcut: psc)","payloadSlotCount [robotName or \"default\"] [echoString]"},
{"payloadSlotCountLocal (shortcut: pscl)","payloadslotcountlocal"},
{"play","play <path_file>"},
{"popupSimple","popupSimple <\"title\"> <\"message\"> <\"buttonLabel\"> <timeout>"},
{"queryDockStatus","queryDockStatus"},
{"queryFaults (shortcut: qf)","queryFaults [robotName or \"default\"] [echoString]"},
{"queryMotors","queryMotors"},
{"queueCancel (shortcut: qc)","queueCancel <type> <value> [echoString or \"default\"] [reason]"},
{"queueCancelLocal (shortcut: qcl)","queueCancelLocal <type> <value> [echoString] [reason]"},
{"queueDropoff (shortcut: qd)","queueDropoff <goalName> [priority] [jobId]"},
{"queueModify (shortcut: qmod)","queueModify <id> <type> <value>"},
{"queueModifyLocal (shortcut: qmodl)","queueModifyLocal <id> <type> <value>"},
{"queueMulti (shortcut: qm)","queueMulti <number of goals> <number of fields per goal> <goal1> <goal1 args> <goal2> <goal2args> … <goalN> <goalN args> [jobid]"},
{"queuePickup (shortcut: qp)","queuePickup <goalName> [priority or \"default\"] [jobId]"},
{"queuePickupDropoff (shortcut: qpd)","queuePickupDropoff <goal1Name> <goal2Name> [priority1 or \"default\"] [priority2 or \"default\"][jobId]"},
{"queueQuery (shortcut: qq)","queueQuery <type> <value> [echoString]"},
{"queueQueryLocal (shortcut: qql)","queueQueryLocal <type> <value> [echoString]"},
{"queueShow (shortcut: qs)","queueShow [echoString]"},
{"queueShowCompleted (shortcut: qsc)","queueshowcompleted [echoString]"},
{"queueShowRobot (shortcut: qsr)","queueShowRobot [robotName or \"default\"] [echoString]"},
{"queueShowRobotLocal (shortcut: qsrl)","queueshowrobotlocal [echo_string]"},
{"quit","quit"},
{"say","say <text_string>"},
{"shutdown","shutdown"},
{"status","status"},
{"stop","stop"},
{"tripReset (shortcut: tr)","tripReset"},
{"undock","undock"},
{"updateInfo","updateInfo <infoName> <infoValue>"},
{"waitTaskCancel","waitTaskCancel"},
{"waitTaskState","waitTaskState"}

        };
        public Dictionary<string, string> ARCLCommands_2016_ARCL_en { get; } = new Dictionary<string, string>()
        {
{"analogInputList","analogInputList"},
{"analogInputQueryRaw","analogInputQueryRaw <name>"},
{"analogInputQueryVoltage","analogInputQueryVoltage <name>"},
{"applicationFaultClear","applicationFaultClear <name>"},
{"applicationFaultQuery","applicationFaultQuery"},
{"applicationFaultSet","applicationFaultSet <name> \"<short_description>\" \"<long_description>\" <bool_driving> <bool_critical>"},
{"arclSendText","arclSendText <string>"},
{"clearAllObstacles","clearAllObstacles"},
{"configAdd","configadd <section>configadd <configuration> <value>"},
{"configParse","configParse"},
{"configStart","configstart"},
{"connectOutgoing","connectOutgoing <hostname> <port>"},
{"createInfo","createInfo <infoName> <maxLen> <infoValue>"},
{"customReadingAddAbsolute","customReadingAddAbsolute<name> <X> <Y>"},
{"customReadingAdd","customReadingAdd<name> <X> <Y>"},
{"customReadingsClear","customReadingsClear<name>"},
{"distanceBetween","distancebetween <FromGoal> <ToGoal>"},
{"distanceFromHere","distanceFromHere <ToGoal>"},
{"dock","dock"},
{"doTask","doTask <task> <argument>"},
{"doTaskInstant","doTaskInstant <task> <argument>"},
{"echo","echo [state]"},
{"enableMotors","enableMotors"},
{"etaRequest","etaRequest"},
{"executeMacro","executeMacro <macro_name >"},
{"faultsGet","faultsGet"},
{"follow","follow"},
{"getConfigSectionInfo","getConfigSectionInfo <section>"},
{"getConfigSectionList","getConfigSectionList"},
{"getConfigSectionValues","getConfigSectionValues <section>"},
{"getDateTime","getDateTime"},
{"getGoals","getGoals"},
{"getInfo","getInfo <infoName>"},
{"getInfoList","getInfoList"},
{"getMacros","getmacros"},
{"getPayload","getPayload"},
{"getPrecedence","getprecedence"},
{"getRoutes","getRoutes"},
{"goto","goto <goal_name> [heading]"},
{"gotoPoint","gotoPoint <X> <Y> <heading: optional>"},
{"gotoRouteGoal","gotoRouteGoal <route_name> <goal_name> [index]"},
{"help","help"},
{"inputList","inputList"},
{"inputQuery","inputQuery <name>"},
{"listAdd","listAdd <task> <argument>"},
{"listExecute","listExecute"},
{"listStart","listStart"},
{"localizeToPoint","localizeToPoint <X> <Y> <T> [xySpread] [thSpread]"},
{"log","log <message> [level]"},
{"mapObjectInfo","mapObjectInfo <name>"},
{"mapObjectList","mapObjectList <type>"},
{"mapObjectTypeInfo","mapObjectTypeInfo <type>"},
{"mapObjectTypeList","mapObjectTypeList"},
{"newConfigParam","newConfigParam <section> <name> <description> <priority_level> <type> <default_value> <min><max> <DisplayHint>"},
{"newConfigSectionComment","newConfigSectionComment <section> <comment>"},
{"odometer","odometer"},
{"odometerReset","odometerReset"},
{"oneLineStatus","oneLineStatus"},
{"outputList","outputList"},
{"outputOff","outputOff <name>"},
{"outputOn","outputOn <name>"},
{"outputQuery","outputQuery <name>"},
{"patrol","patrol <route_name>"},
{"patrolOnce","patrolOnce <route_name> [index]"},
{"patrolResume","patrolResume [route_name]"},
{"pauseTaskCancel","pauseTaskCancel"},
{"pauseTaskState","pauseTaskState"},
{"payloadQuery (shortcut: pq)","payloadQuery [robotName or \"default\"] [slotNumber or \"default\"] [echoString]"},
{"payloadQueryLocal (shortcut: pql)","payloadQueryLocal [slotNumber or \"default\"] [echoString]"},
{"payloadRemove (shortcut: pr)","payloadRemove <slot_number>"},
{"payloadSet (shortcut: ps)","payloadSet <slot_number> <slot_string>"},
{"payloadSlotCount (shortcut: psc)","payloadSlotCount [robotName or \"default\"] [echoString]"},
{"payloadSlotCountLocal (shortcut: pscl)","payloadslotcountlocal"},
{"play","play <path_file>"},
{"popupSimple","popupSimple <\"title\"> <\"message\"> <\"buttonLabel\"> <timeout>"},
{"queryDockStatus","queryDockStatus"},
{"queryFaults (shortcut: qf)","queryFaults [robotName or \"default\"] [echoString]"},
{"queryMotors","queryMotors"},
{"queueCancel (shortcut: qc)","queueCancel <type> <value> [echoString or \"default\"] [reason]"},
{"queueCancelLocal (shortcut: qcl)","queueCancelLocal <type> <value> [echoString] [reason]"},
{"queueDropoff (shortcut: qd)","queueDropoff <goalName> [priority] [jobId]"},
{"queueModify (shortcut: qmod)","queueModify <id> <type> <value>"},
{"queueModifyLocal (shortcut: qmodl)","queueModifyLocal <id> <type> <value>"},
{"queueMulti (shortcut: qm)","queueMulti <number of goals> <number of fields per goal> <goal1> <goal1 args> <goal2> <goal2args> … <goalN> <goalN args> [jobid]"},
{"queuePickup (shortcut: qp)","queuePickup <goalName> [priority or \"default\"] [jobId]"},
{"queuePickupDropoff (shortcut: qpd)","queuePickupDropoff <goal1Name> <goal2Name> [priority1 or \"default\"] [priority2 or \"default\"][jobId]"},
{"queueQuery (shortcut: qq)","queueQuery <type> <value> [echoString]"},
{"queueQueryLocal (shortcut: qql)","queueQueryLocal <type> <value> [echoString]"},
{"queueShow (shortcut: qs)","queueShow [echoString]"},
{"queueShowCompleted (shortcut: qsc)","queueshowcompleted [echoString]"},
{"queueShowRobot (shortcut: qsr)","queueShowRobot [robotName or \"default\"] [echoString]"},
{"queueShowRobotLocal (shortcut: qsrl)","queueshowrobotlocal [echo_string]"},
{"quit","quit"},
{"rangeDeviceGetCumulative","rangeDeviceGetCumulative <name>"},
{"rangeDeviceGetCurrent","rangeDeviceGetCurrent <name>"},
{"rangeDeviceList","rangeDeviceList"},
{"say","say <text_string>"},
{"scanAddGoal","scanAddGoal <name> [description]"},
{"scanAddInfo","scanaddinfo <LogInfo:type> <Name=string> <Label=string> <Desc=string> [Size =integer][IsData=integer] [Vis=mode] [FtSize=size] [Colorn=value] [Shape=shape]"},
{"scanAddTag","scanAddTag cairn:<name> [label] [icon_type] [description]"},
{"scanStart","scanStart <name>"},
{"scanStop","scanStop"},
{"setPayload","setPayload <payload>"},
{"setPrecedence","setPrecedence <integer>"},
{"shutdown","shutdown"},
{"status","status"},
{"stop","stop"},
{"trackSectors","trackSectors"},
{"trackSectorsAtGoal","trackSectorsAtGoal <goal>"},
{"trackSectorsAtPoint","trackSectorsAtPoint <X> <Y>"},
{"trackSectorsPath","trackSectorsPath [distance]"},
{"undock","undock"},
{"updateInfo","updateInfo <infoName> <infoValue>"},
{"waitTaskCancel","waitTaskCancel"},
{"waitTaskState","waitTaskState"}
        };

    }








}