using System;
using System.Collections.Generic;
using HotelBooking.Core;
using Moq;

namespace HotelBooking.UnitTests.Fakes
{
    public static class MockBookingRepositoryFactory
    {
        public static Mock<IRepository<Booking>> Create(DateTime fullyOccupiedStart, DateTime fullyOccupiedEnd)
        {
            var mock = new Mock<IRepository<Booking>>();

            // Equivalent of GetAsync
            mock.Setup(r => r.GetAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking
                {
                    Id = 1,
                    StartDate = fullyOccupiedStart,
                    EndDate = fullyOccupiedEnd,
                    IsActive = true,
                    CustomerId = 1,
                    RoomId = 1
                });

            // Equivalent of GetAllAsync
            mock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Booking>
                {
                    new Booking { Id = 1, StartDate = DateTime.Today.AddDays(1), EndDate = DateTime.Today.AddDays(1), IsActive = true, CustomerId = 1, RoomId = 1 },
                    new Booking { Id = 1, StartDate = fullyOccupiedStart, EndDate = fullyOccupiedEnd, IsActive = true, CustomerId = 1, RoomId = 1 },
                    new Booking { Id = 2, StartDate = fullyOccupiedStart, EndDate = fullyOccupiedEnd, IsActive = true, CustomerId = 2, RoomId = 2 },
                });

            // AddAsync, EditAsync, RemoveAsync need no Setup to "work" —
            // Moq returns Task.CompletedTask by default for Task-returning
            // methods with no Setup. Call-tracking is done via Verify()
            // instead of the fake's boolean fields (see usage below).

            return mock;
        }
    }
}