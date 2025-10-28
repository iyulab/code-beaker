"""
Bug Type: Logic error
Description: Wrong condition in factorial calculation
Expected: factorial(5) = 120
Actual: Returns incorrect value due to wrong base case
"""

def factorial(n):
    """Calculate factorial of n"""
    # BUG: should be n <= 1, not n < 1
    if n < 1:
        return 1
    return n * factorial(n - 1)

# Test cases
print(f"factorial(0) = {factorial(0)}")  # Should be 1
print(f"factorial(1) = {factorial(1)}")  # Should be 1
print(f"factorial(5) = {factorial(5)}")  # Should be 120

# This will cause infinite recursion for n=1!
