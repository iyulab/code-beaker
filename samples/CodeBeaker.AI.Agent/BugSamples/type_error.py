"""
Bug Type: Type error
Description: String concatenation with integer
Expected: Should calculate average properly
Actual: TypeError when trying to concatenate string with number
"""

def calculate_average(numbers):
    """Calculate average of numbers"""
    total = sum(numbers)
    count = len(numbers)
    avg = total / count

    # BUG: Should use f-string or str() conversion
    return "Average: " + avg

# Test case
numbers = [10, 20, 30, 40, 50]
try:
    result = calculate_average(numbers)
    print(result)
except TypeError as e:
    print(f"Error: {e}")
