Feature: Create booking
  A room can be booked for a future period (start date - end date)
  if it is not already booked on one or more days of the period.
  Dates are given as days from today (T), e.g. 10 means T+10.

  Scenario Outline: Invalid dates (equivalence classes and boundary values)
    Given all rooms are fully occupied from day 10 to day 20
    When I create a booking from day <start> to day <end>
    Then the booking is rejected with an invalid dates error

    Examples:
      | start | end | case                |
      | -3    | 3   | EC2: SD in the past |
      | 5     | 2   | EC3: SD after ED    |
      | 0     | 5   | BV: SD = today      |
      | -1    | 5   | BV: SD = yesterday  |
      | 2     | 1   | BV: SD = ED + 1     |

  Scenario Outline: Valid dates in the future (equivalence class and boundary value)
    Given all rooms are fully occupied from day 10 to day 20
    When I create a booking from day <start> to day <end>
    Then the booking is created

    Examples:
      | start | end | case                          |
      | 1     | 3   | EC1: valid dates              |
      | 1     | 1   | BV: SD = tomorrow and SD = ED |

  Scenario Outline: Booking against a fully occupied range (decision table)
    Given all rooms are fully occupied from day 10 to day 20
    When I create a booking from day <start> to day <end>
    Then the booking is <result>

    Examples:
      | start | end | result   | case                         |
      | 8     | 9   | created  | 1: SD in B, ED in B          |
      | 21    | 22  | created  | 2: SD in A, ED in A          |
      | 9     | 21  | rejected | 3: SD in B, ED in A          |
      | 9     | 10  | rejected | 4: SD in B, ED in O (ED = s) |
      | 9     | 20  | rejected | 5: SD in B, ED in O (ED = e) |
      | 10    | 21  | rejected | 6: SD in O (SD = s), ED in A |
      | 20    | 21  | rejected | 7: SD in O (SD = e), ED in A |
      | 10    | 10  | rejected | 8: SD in O, ED in O (s, s)   |
      | 20    | 20  | rejected | 9: SD in O, ED in O (e, e)   |
      | 10    | 20  | rejected | 10: SD in O, ED in O (s, e)  |
