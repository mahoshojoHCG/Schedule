﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using static HitRefresh.Schedule.ScheduleStatic;

namespace HitRefresh.Schedule
{
    /// <summary>
    ///     课程代表条目内容，即课程的时间。
    /// </summary>
    public class CourseContent : IComparable<CourseContent>
    {
        /// <summary>
        ///     新建一个课程条目的子条目
        /// </summary>
        /// <param name="courseName"></param>
        /// <param name="dayOfWeek"></param>
        /// <param name="courseTime"></param>
        /// <param name="isLongCourse"></param>
        /// <param name="isLab"></param>
        /// <param name="weekExpression"></param>
        [JsonConstructor]
        internal CourseContent(string courseName, DayOfWeek dayOfWeek, CourseTime courseTime, bool isLongCourse,
            bool isLab, string weekExpression)
        {
            CourseName = courseName;
            DayOfWeek = dayOfWeek;
            CourseTime = courseTime;
            IsLongCourse = isLongCourse;
            IsLab = isLab;

            //Parse Week Expression
            weekExpression = weekExpression.RemoveCommaSpace();

            var currentTeacher = "";
            var timeStack = new Stack<string>();
            var timeTeacherMap = new Dictionary<string, string>();
            var timeLocationMap = new Dictionary<string, string>();

            foreach (var match in ScheduleExpressionUnitRegex.Matches(weekExpression))
            {
                var unit = match?.ToString();
                if (unit == null)
                    continue;
                var unitType = LocationRegex.IsMatch(unit) ? ScheduleExpressionUnitType.Location :
                    TeacherNameRegex.IsMatch(unit) ? ScheduleExpressionUnitType.Teacher :
                    CourseTimeRegex.IsMatch(unit) ? ScheduleExpressionUnitType.Time :
                    ScheduleExpressionUnitType.Unknown;

                switch (unitType)
                {
                    case ScheduleExpressionUnitType.Teacher:
                        currentTeacher = unit;
                        break;
                    case ScheduleExpressionUnitType.Time:
                        timeTeacherMap.Add(unit, currentTeacher);
                        timeStack.Push(unit);
                        break;
                    case ScheduleExpressionUnitType.Location:
                        while (timeStack.Count > 0) timeLocationMap.Add(timeStack.Pop(), unit);
                        break;
                    case ScheduleExpressionUnitType.Unknown:
                        throw new ArgumentException(weekExpression, nameof(weekExpression), null);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            while (timeStack.Count > 0) timeLocationMap.Add(timeStack.Pop(), "<地点待定>");

            foreach (var time in timeTeacherMap.Keys)
            foreach (var weekIndex in time.ToIntSequence())
                WeekInformation.Add(weekIndex, new()
                {
                    Name = CourseName,
                    Teacher = timeTeacherMap[time],
                    Location = timeLocationMap[time]
                });
        }

        /// <summary>
        ///     课程名称
        /// </summary>
        public string CourseName { get; init; }

        /// <summary>
        ///     课程最大持续周数
        /// </summary>
        [JsonIgnore]
        public int MaxWeek => WeekInformation.Keys.Max();

        /// <summary>
        ///     是否为实验课
        /// </summary>
        public bool IsLab { get; set; }

        /// <summary>
        ///     在周几上课
        /// </summary>
        public DayOfWeek DayOfWeek { get; set; }


        /// <summary>
        ///     课程的时间(第几节课)
        /// </summary>
        public CourseTime CourseTime { get; set; }

        /// <summary>
        ///     是否是两节连在一起那种课
        /// </summary>
        public bool IsLongCourse
        {
            get => Length == new TimeSpan(3, 30, 00);
            set =>
                Length = !value
                    ? new(1, 45, 00)
                    : new(3, 30, 00);
        }

        /// <summary>
        ///     课程的长度
        /// </summary>
        [JsonIgnore]
        public TimeSpan Length { get; private set; }

        /// <summary>
        ///     课程开始的时间距离0点的时长
        /// </summary>
        [JsonIgnore]
        public TimeSpan StartTime => StartTimes[(int) CourseTime];

        /// <summary>
        ///     周数信息，包含上课的周和对应的教师、教室
        ///     AF: int周数->(str教师,str教室)
        /// </summary>
        private Dictionary<int, CourseCell> WeekInformation { get; } = new();

        /// <summary>
        ///     返回一个按照周的切片
        /// </summary>
        /// <param name="i">周数</param>
        /// <returns>该周的课，如果无课则返回为课名称为空</returns>
        public CourseCell this[int i] => Slice(i);

        /// <summary>
        ///     枚举课程的基本信息
        /// </summary>
        /// <returns>课程基本信息的不可变数组</returns>
        public ImmutableArray<KeyValuePair<int, CourseCell>> EnumerateInformation()
        {
            return WeekInformation.ToImmutableArray();
        }

        /// <summary>
        ///     返回一个按照周的切片
        /// </summary>
        /// <param name="i">周数</param>
        /// <returns>该周的课，如果无课则返回为课名称为空</returns>
        public CourseCell Slice(int i)
        {
            return WeekInformation.ContainsKey(i) ? WeekInformation[i] : new();
        }

        /// <inheritdoc />
        public int CompareTo(CourseContent? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return (int) DayOfWeek * 10 + CourseTime - (int) other.DayOfWeek * 10 - other.CourseTime;
        }
    }
}